using System.Runtime.CompilerServices;

// Exposes internal helpers (pure parsers, calculators and option builders) to the unit-test project.
[assembly: InternalsVisibleTo("audit-tests")]
