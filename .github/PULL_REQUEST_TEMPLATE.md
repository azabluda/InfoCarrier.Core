<!--
  CONTRIBUTING.md has the build and test commands. Two gates decide whether a change can land, and
  which of them applies depends only on what the change touches:

    src/           both dotnet run --file eng/Measure.cs -- and dotnet run --file eng/TrimRatchet.cs --
    test/ only     dotnet run --file eng/Measure.cs --
    docs or text   neither

  A change to src/ also has to survive `CI=true dotnet build InfoCarrier.Core.slnx --configuration
  Release`, which is what the server runs. The Release part is not optional.
-->

## What this changes

## Evidence

<!--
  For a change to src/ or test/, paste the numbers rather than describing them: the failure count,
  and what the run fixed and broke. A count that did not move cannot tell "fixed four, broke four"
  from "changed nothing", which is why dotnet run --file eng/Measure.cs -- prints all three levels.
-->

- [ ] `dotnet run --file eng/Measure.cs --` run, and the fixed/broken lists read, not just the count
- [ ] `dotnet run --file eng/TrimRatchet.cs --` green, if `src/` changed
- [ ] The plan checkbox in `docs/plans/v10/implementation-plan.md` updated in this same commit
