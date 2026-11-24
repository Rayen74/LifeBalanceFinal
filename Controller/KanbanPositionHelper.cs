namespace Controller;

/// <summary>
/// Pure calculation helper for determining zero-based task insertion positions in Kanban columns.
/// Handles drag-and-drop reordering within columns, cross-column moves, empty column drops,
/// and prevents off-by-one errors when items shift during repositioning.
/// </summary>
public static class KanbanPositionHelper
{
    /// <summary>
    /// Calculates the zero-based target position for MoveTaskAsync when dropping a task
    /// relative to an existing target card or empty column space.
    /// </summary>
    /// <param name="sourceColumnId">Column where dragged task originated.</param>
    /// <param name="targetColumnId">Destination column.</param>
    /// <param name="sourcePosition">Original zero-based position in source column.</param>
    /// <param name="targetCardPosition">Position of the card hovered/dropped onto, or null if dropped onto empty space.</param>
    /// <param name="dropAfter">True if dropped in the bottom half of target card, false if in top half.</param>
    /// <param name="targetColumnTaskCount">Total number of tasks currently in target column.</param>
    public static int CalculateDropPosition(
        Guid sourceColumnId,
        Guid targetColumnId,
        int sourcePosition,
        int? targetCardPosition,
        bool dropAfter,
        int targetColumnTaskCount)
    {
        bool isSameColumn = sourceColumnId == targetColumnId;

        // 1. Dropped onto empty column space (not onto a specific card)
        if (!targetCardPosition.HasValue)
        {
            if (isSameColumn)
            {
                // Moving to end of same column
                return Math.Max(0, targetColumnTaskCount - 1);
            }
            // Moving to end of another column
            return targetColumnTaskCount;
        }

        // 2. Target index before adjusting for self-removal
        int desiredIndex = dropAfter ? targetCardPosition.Value + 1 : targetCardPosition.Value;

        if (isSameColumn)
        {
            // When moving within the same column downwards (from smaller sourcePosition to greater index),
            // removing the task from sourcePosition shifts items above sourcePosition down by 1 index.
            // MoveTaskAsync removes the item first before inserting: targetColumnTasks.Insert(targetPosition, task).
            if (desiredIndex > sourcePosition)
            {
                desiredIndex--;
            }
        }

        int maxIndex = isSameColumn ? Math.Max(0, targetColumnTaskCount - 1) : targetColumnTaskCount;
        return Math.Clamp(desiredIndex, 0, maxIndex);
    }
}
