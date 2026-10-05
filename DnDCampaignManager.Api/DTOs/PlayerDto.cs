namespace DnDCampaignManager.Api.DTOs
{
    public record AddPlayerDto(
        [System.ComponentModel.DataAnnotations.Required,
                   System.ComponentModel.DataAnnotations.EmailAddress] string Email);

    public record RemovePlayerDto(string Email);
}
