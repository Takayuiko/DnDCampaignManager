import type { CharacterSectionProps, CharacterFieldChange } from "./characterTypes";
import { sectionClass, sectionTitleClass } from "./characterSectionStyles";
import { ALIGNMENT_OPTIONS, isOfficialClass, isOfficialRace, isOfficialBackground } from "./characterConstants";
import type { CharacterOptionsState } from "./useCharacterOptions";

export default function IdentitySection({ form, setForm, handleChange, options }: CharacterSectionProps & { handleChange: CharacterFieldChange } & { options: CharacterOptionsState }) {
    const { names: classNames, loading: classLoading, error: classError, adding: addingClass, setAdding: setAddingClass, draft: newClassName, setDraft: setNewClassName, add: handleAddClass } = options.class;
    const { names: raceNames, loading: raceLoading, error: raceError, adding: addingRace, setAdding: setAddingRace, draft: newRaceName, setDraft: setNewRaceName, add: handleAddRace } = options.race;
    const { names: backgroundNames, loading: backgroundLoading, error: backgroundError, adding: addingBackground, setAdding: setAddingBackground, draft: newBackgroundName, setDraft: setNewBackgroundName, add: handleAddBackground } = options.background;
    return (<>
        {/* Identity */}
        <section className={sectionClass}>
            <h2 className={sectionTitleClass}>Identity</h2>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="md:col-span-2">
                    <label className="block text-sm font-semibold text-stone-700">
                        Character Name
                    </label>
                    <input
                        name="name"
                        maxLength={120}
                        value={form.name}
                        onChange={handleChange}
                        className="w-full border border-stone-400 rounded-md p-2 bg-white"
                        required
                    />
                </div>

                <div>
                    <label className="block text-sm font-semibold text-stone-700">
                        Level
                    </label>
                    <input
                        type="number"
                        name="level"
                        min={1}
                        max={100}
                        value={form.level}
                        onChange={handleChange}
                        className="w-full border border-stone-400 rounded-md p-2 bg-white"
                    />
                </div>

                {/* Class + Homebrew */}
                <div>
                    <div className="flex items-center justify-between">
                        <label className="block text-sm font-semibold text-stone-700">
                            Class
                        </label>

                        <button
                            type="button"
                            className="text-xs font-semibold text-emerald-800 hover:underline"
                            onClick={() => setAddingClass((v) => !v)}
                        >
                            {addingClass ? "Close" : "Add homebrew"}
                        </button>
                    </div>

                    <select
                        name="class"
                        value={form.class}
                        onChange={handleChange}
                        className="w-full border border-stone-400 rounded-md p-2 bg-white"
                        required
                        disabled={classLoading}
                    >
                        <option value="" disabled>
                            {classLoading ? "Summoning classes..." : "Choose a class"}
                        </option>

                        <optgroup label="Official">
                            {classNames
                                .filter((c) => isOfficialClass(c))
                                .map((cls) => (
                                    <option key={cls} value={cls}>
                                        {cls}
                                    </option>
                                ))}
                        </optgroup>

                        <optgroup label="Homebrew">
                            {classNames
                                .filter((c) => !isOfficialClass(c))
                                .map((cls) => (
                                    <option key={cls} value={cls}>
                                        {cls}
                                    </option>
                                ))}
                        </optgroup>
                    </select>

                    {classError && <p className="mt-1 text-xs text-red-700">{classError}</p>}

                    {addingClass && (
                        <div className="mt-2 rounded-lg border border-stone-300 bg-amber-50 p-3">
                            <p className="text-xs text-stone-600 mb-2">
                                Add a homebrew class to this campaign’s roster.
                            </p>

                            <div className="flex gap-2">
                                <input
                                    value={newClassName}
                                    onChange={(e) => setNewClassName(e.target.value)}
                                    placeholder="e.g. Blood Hunter"
                                    className="flex-1 border border-stone-400 rounded-md p-2 bg-white"
                                />
                                <button
                                    type="button"
                                    onClick={handleAddClass}
                                    disabled={options.class.saving}
                                    className="px-3 py-2 text-sm font-semibold rounded-lg bg-emerald-700 text-white hover:bg-emerald-600"
                                >
                                    Inscribe
                                </button>
                            </div>
                        </div>
                    )}
                </div>

                {/* Race + Homebrew */}
                <div>
                    <div className="flex items-center justify-between">
                        <label className="block text-sm font-semibold text-stone-700">
                            Race
                        </label>

                        <button
                            type="button"
                            className="text-xs font-semibold text-emerald-800 hover:underline"
                            onClick={() => setAddingRace(v => !v)}
                        >
                            {addingRace ? "Close" : "Add homebrew"}
                        </button>
                    </div>

                    <select
                        name="race"
                        value={form.race}
                        onChange={handleChange}
                        className="w-full border border-stone-400 rounded-md p-2 bg-white"
                        required
                        disabled={raceLoading}
                    >
                        <option value="" disabled>
                            {raceLoading ? "Summoning races..." : "Choose a race"}
                        </option>

                        <optgroup label="Official">
                            {raceNames.filter(isOfficialRace).map(r => (
                                <option key={r} value={r}>{r}</option>
                            ))}
                        </optgroup>

                        <optgroup label="Homebrew">
                            {raceNames.filter(r => !isOfficialRace(r)).map(r => (
                                <option key={r} value={r}>{r}</option>
                            ))}
                        </optgroup>
                    </select>

                    {raceError && <p className="mt-1 text-xs text-red-700">{raceError}</p>}

                    {addingRace && (
                        <div className="mt-2 rounded-lg border border-stone-300 bg-amber-50 p-3">
                            <p className="text-xs text-stone-600 mb-2">
                                Add a homebrew race to this campaign’s roster.
                            </p>

                            <div className="flex gap-2">
                                <input
                                    value={newRaceName}
                                    onChange={(e) => setNewRaceName(e.target.value)}
                                    placeholder="e.g. Aasimar"
                                    className="flex-1 border border-stone-400 rounded-md p-2 bg-white"
                                />
                                <button
                                    type="button"
                                    onClick={handleAddRace}
                                    disabled={options.race.saving}
                                    className="px-3 py-2 text-sm font-semibold rounded-lg bg-emerald-700 text-white hover:bg-emerald-600"
                                >
                                    Inscribe
                                </button>
                            </div>
                        </div>
                    )}
                </div>

                {/* Background + Homebrew */}
                <div>
                    <div className="flex items-center justify-between">
                        <label className="block text-sm font-semibold text-stone-700">
                            Background
                        </label>

                        <button
                            type="button"
                            className="text-xs font-semibold text-emerald-800 hover:underline"
                            onClick={() => setAddingBackground((v) => !v)}
                        >
                            {addingBackground ? "Close" : "Add homebrew"}
                        </button>
                    </div>

                    <select
                        name="background"
                        value={form.background}
                        onChange={handleChange}
                        className="w-full border border-stone-400 rounded-md p-2 bg-white"
                        disabled={backgroundLoading}
                    >
                        <option value="" disabled>
                            {backgroundLoading ? "Summoning backgrounds..." : "Choose a background"}
                        </option>

                        <optgroup label="Official">
                            {backgroundNames
                                .filter((b) => isOfficialBackground(b))
                                .map((b) => (
                                    <option key={b} value={b}>
                                        {b}
                                    </option>
                                ))}
                        </optgroup>

                        <optgroup label="Homebrew">
                            {backgroundNames
                                .filter((b) => !isOfficialBackground(b))
                                .map((b) => (
                                    <option key={b} value={b}>
                                        {b}
                                    </option>
                                ))}
                        </optgroup>
                    </select>

                    {backgroundError && <p className="mt-1 text-xs text-red-700">{backgroundError}</p>}

                    {addingBackground && (
                        <div className="mt-2 rounded-lg border border-stone-300 bg-amber-50 p-3">
                            <p className="text-xs text-stone-600 mb-2">
                                Add a homebrew background to this campaign’s roster.
                            </p>

                            <div className="flex gap-2">
                                <input
                                    value={newBackgroundName}
                                    onChange={(e) => setNewBackgroundName(e.target.value)}
                                    placeholder="e.g. Haunted One"
                                    className="flex-1 border border-stone-400 rounded-md p-2 bg-white"
                                />
                                <button
                                    type="button"
                                    onClick={handleAddBackground}
                                    disabled={options.background.saving}
                                    className="px-3 py-2 text-sm font-semibold rounded-lg bg-emerald-700 text-white hover:bg-emerald-600"
                                >
                                    Inscribe
                                </button>
                            </div>
                        </div>
                    )}
                </div>

                {/* Alignment */}
                <div>
                    <label className="block text-sm font-semibold text-stone-700">
                        Alignment
                    </label>

                    <select
                        name="alignment"
                        value={form.alignment}
                        onChange={(e) =>
                            setForm((prev) => ({ ...prev, alignment: e.target.value }))
                        }
                        className="w-full border border-stone-400 rounded-md p-2 bg-white"
                    >
                        <option value="">Choose alignment</option>
                        {ALIGNMENT_OPTIONS.map((a) => (
                            <option key={a} value={a}>
                                {a}
                            </option>
                        ))}
                    </select>

                    <p className="mt-1 text-xs text-stone-500">
                        “Neutral” = True Neutral.
                    </p>
                </div>

                {/* XP */}
                <div>
                    <label className="block text-sm font-semibold text-stone-700">XP</label>
                    <input
                        type="number"
                        name="experiencePoints"
                        max={1000000000}
                        min={0}
                        value={form.experiencePoints}
                        onChange={handleChange}
                        className="w-full border border-stone-400 rounded-md p-2 bg-white"
                    />
                </div>
            </div>
        </section>
    </>);
}
