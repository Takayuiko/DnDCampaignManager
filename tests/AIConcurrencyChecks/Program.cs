using DnDCampaignManager.Api.Services.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine($"PASS: {message}"); }
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
    ["OpenAI:Limits:MaxConcurrentRequests"] = "1",
    ["OpenAI:Limits:MaxQueuedRequests"] = "1",
    ["OpenAI:Limits:ConcurrencyWaitSeconds"] = "1"
}).Build();
using var gate = new AIConcurrencyLimiter(config);
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
var active = await gate.AcquireAsync(deadline.Token);
var queued = gate.AcquireAsync(deadline.Token).AsTask();
Check(!queued.IsCompleted, "An occupied slot queues the next operation");
try { using var rejected = await gate.AcquireAsync(deadline.Token); throw new Exception("Queue was unbounded"); }
catch (AIBusyException) { Check(!queued.IsCompleted, "A full queue rejects excess load without displacing waiting work"); }
active.Dispose();
using (var next = await queued.WaitAsync(deadline.Token))
{
    try { using var lease = await gate.AcquireAsync(deadline.Token); throw new Exception("Queue wait was unbounded"); }
    catch (AIBusyException) { Check(true, "Queued work has a bounded wait and a retryable busy response"); }
    using var cancelled = new CancellationTokenSource();
    var waiter = gate.AcquireAsync(cancelled.Token).AsTask(); cancelled.Cancel();
    try { using var lease = await waiter; throw new Exception("Cancellation was ignored"); }
    catch (OperationCanceledException) { Check(true, "Caller cancellation removes waiting work"); }
    var afterCancellation = gate.AcquireAsync(deadline.Token).AsTask();
    Check(!afterCancellation.IsCompleted, "Cancellation and timeout free queue capacity without releasing active work");
    next.Dispose();
    using var recovered = await afterCancellation.WaitAsync(deadline.Token);
}
using (var available = await gate.AcquireAsync(deadline.Token))
    Check(available.IsAcquired, "Released permits are reusable after rejection, cancellation and timeout");
using var preCancelled = new CancellationTokenSource(); preCancelled.Cancel();
try { using var lease = await gate.AcquireAsync(preCancelled.Token); throw new Exception("Cancelled work acquired a slot"); }
catch (OperationCanceledException) { Check(true, "Already cancelled work cannot consume capacity"); }
var services = new ServiceCollection(); services.AddSingleton<IConfiguration>(config); services.AddCampaignAI(config);
using var provider = services.BuildServiceProvider();
using var firstScope = provider.CreateScope(); using var secondScope = provider.CreateScope();
Check(ReferenceEquals(firstScope.ServiceProvider.GetRequiredService<AIConcurrencyLimiter>(),
    secondScope.ServiceProvider.GetRequiredService<AIConcurrencyLimiter>()), "All request scopes share the same process-wide limiter");
Console.WriteLine("All AI concurrency checks passed. No database or paid provider calls.");
