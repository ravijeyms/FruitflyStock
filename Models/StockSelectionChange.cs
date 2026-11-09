// ===== Result checkbox event =====
// Pseudocode:
// - Carry the affected stock and whether its checkbox is now selected to the parent.
// A sealed record is appropriate for this immutable value-like event payload: it has no
// mutable state or intended inheritance hierarchy.
public sealed record StockSelectionChange(FlyStock Stock, bool IsSelected);
