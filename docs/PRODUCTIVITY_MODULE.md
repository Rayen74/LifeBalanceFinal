# LifeBalance — Productivity Module Specification & Architecture

This document describes the design, architecture, database schema, business rules, and UI integration of the **Productivity Module** in LifeBalance, including the **Phase 1 Productivity Workflow Improvements**.

---

## 1. Domain Entities & Database Schema

The Productivity module implements three primary sub-domains:
1. **Kanban Boards & Tasks** (`TaskBoard`, `TaskColumn`, `TaskItem`)
2. **Personal Goals & Task Links** (`PersonalGoal`, `TaskGoal`)
3. **Daily Habits & Streak Tracking** (`Habit`, `HabitCompletion`)

### PostgreSQL Schema & Relationships

```
 users (id)
   ▲
   ├──< task_boards (id, user_id, name, description, created_at)
   │        ▲
   │        └──< task_columns (id, board_id, name, position, is_completed)
   │                  ▲
   │                  └──< task_items (id, column_id, title, description, priority, due_date, estimated_duration, position, completed_at, created_at)
   │                             ▲
   │                             ├──< task_goals (task_id, goal_id) >──┐
   │                                                                   ▼
   ├──< personal_goals (id, user_id, name, description, target_date, status, created_at)
   │
   └──< habits (id, user_id, name, description, frequency, start_date, is_active, created_at)
            ▲
            └──< habit_completions (id, habit_id, completed_on) [UNIQUE (habit_id, completed_on)]
```

### Table Specifications

| Table | Primary Key | Foreign Keys | Key Constraints / Indexes |
|---|---|---|---|
| `task_boards` | `id` (UUID) | `user_id` -> `users(id)` (Cascade) | Index on `user_id` |
| `task_columns` | `id` (UUID) | `board_id` -> `task_boards(id)` (Cascade) | Index on `board_id`, ordered by `position` |
| `task_items` | `id` (UUID) | `column_id` -> `task_columns(id)` (Cascade) | Index on `column_id`, ordered by `position` |
| `personal_goals` | `id` (UUID) | `user_id` -> `users(id)` (Cascade) | Index on `user_id` |
| `task_goals` | `(task_id, goal_id)` (Composite) | `task_id` -> `task_items(id)` (Cascade), `goal_id` -> `personal_goals(id)` (Cascade) | Cross-user validation enforced in business service |
| `habits` | `id` (UUID) | `user_id` -> `users(id)` (Cascade) | Index on `user_id` |
| `habit_completions` | `id` (UUID) | `habit_id` -> `habits(id)` (Cascade) | Unique Index `(habit_id, completed_on)` |

---

## 2. Business Logic & Workflow Rules

### Kanban Boards (`BoardService.cs`)
- **Default Columns:** When a board is created, three columns are automatically created:
  - `To Do` (Position: 0, IsCompleted: false)
  - `In Progress` (Position: 1, IsCompleted: false)
  - `Done` (Position: 2, IsCompleted: true)
- **Positioning & Reordering:** Tasks and columns use zero-based consecutive integers (`0, 1, 2...`). Insertion appends or inserts at a specific index, with adjacent positions re-indexed inside an isolated database transaction.
- **Move Lifecycle & Completion Timestamps:**
  - Moving a task to a column where `is_completed = true` stamps `CompletedAt = DateTime.UtcNow`.
  - Moving a task back to an active column clears `CompletedAt = null`.
- **Column Deletion Protection:** A column cannot be deleted if it is the only column on a board. If tasks exist, they must be reassigned to a designated target column before removal.
- **Board Statistics Calculation:** `GetBoardStatisticsAsync` computes dynamic metrics live without database computed columns or locks:
  - `TotalTasks`: All tasks belonging to the selected board.
  - `CompletedTasks`: Tasks in columns where `IsCompleted == true`.
  - `InProgressTasks`: Tasks in non-completed columns *excluding* the first non-completed column (ordered by column `Position`), which represents the backlog / To Do stage.
  - `CompletionPercentage`: `(CompletedTasks / TotalTasks) * 100` rounded to 1 decimal place (0.0% when board is empty).
  - `DueInNext7Days`: Non-completed tasks with `DueDate` between `today` and `today.AddDays(6)` (inclusive 7-day calendar window).
  - `OverdueTasks`: Non-completed tasks with `DueDate < today`.
  - Completed tasks are strictly excluded from overdue and due soon counts.

### Date Abstraction (`ILocalDateProvider.cs`)
- `ILocalDateProvider`: Centralized abstraction for calendar date evaluation.
- `SystemLocalDateProvider`: Production implementation returning `DateOnly.FromDateTime(DateTime.Now)` to ensure user-facing dates align with the machine's local calendar day rather than UTC day shifts.
- `FixedLocalDateProvider`: Deterministic provider for unit and integration testing without system clock dependencies.

### Drag-and-Drop Positioning Logic (`KanbanPositionHelper.cs`)
- Extracted pure calculation helper `KanbanPositionHelper.CalculateDropPosition`:
  - When reordering within the same column downwards, adjusts index to account for self-removal before re-insertion.
  - When moving cross-column, inserts at target index or appends to end of column when dropped on empty space.
  - Dropping into an empty column resolves to position `0`.
  - Dropping back to the identical position is detected as a no-op, avoiding unnecessary database operations.

### Personal Goals & Derived Progress (`GoalService.cs`)
- **Goal Management:** CRUD for goals with `GoalStatus` (`Active = 0`, `Completed = 1`, `Cancelled = 2`).
- **Task Association:** Tasks can be linked to multiple goals (`TaskGoal`) across different user boards via `GetUserTasksAsync`.
- **Strict User Isolation:** The service explicitly verifies that both the `TaskItem` and the `PersonalGoal` belong to the same authenticated `UserId`. Cross-user linking requests are rejected. Duplicate links are safely handled.
- **Derived Goal Progress:**
  $$\text{Progress} = \frac{\text{CompletedLinkedTasks}}{\text{TotalLinkedTasks}} \times 100$$
  - A linked task is considered completed when its parent column has `IsCompleted == true`.
  - Pure calculation in `GoalService.CalculateGoalProgress(PersonalGoal)`.
  - If no tasks are linked (`TotalLinkedTasks == 0`), displays `No tasks linked yet` (avoids showing a misleading `0%` progress bar).
  - Progress updates dynamically whenever tasks are moved between columns in Kanban.
  - `GoalStatus` remains manually controlled and decoupled from derived task progress.

### Habits & Streaks (`HabitService.cs`)
- **Completion Logging:** Recorded as `(HabitId, DateOnly)`.
- **Duplicate Protection:** The unique index `(habit_id, completed_on)` ensures idempotent completion logging.
- **Validation:** Completions cannot be recorded prior to the habit's `StartDate`, or on an inactive habit.
- **Streak Calculation Algorithm:**
  1. Completion dates are extracted and placed into a set.
  2. If today is completed, start scanning consecutively backwards from today.
  3. If today is not yet completed, start scanning consecutively backwards from yesterday (preserving active streak for the current day).
  4. Each consecutive match increments streak counter. The first missing day terminates the count.

---

## 3. Security & Multi-Tenancy Isolation

- **Source of Truth:** PostgreSQL is the only persistent store for productivity data.
- **Multi-Tenant Isolation:** Every service call requires the authenticated user's `Guid userId` obtained from `App.Auth.CurrentUser.Id`.
- **Hierarchical Verification:** For child records (columns, tasks, completions), ownership is verified through parent foreign keys to prevent unauthorized tampering:
  - `TaskItem -> Column -> Board -> UserId == currentUserId`
  - `TaskGoal -> Task.Column.Board.UserId == currentUserId && Goal.UserId == currentUserId`
  - `HabitCompletion -> Habit.UserId == currentUserId`

---

## 4. Avalonia Desktop UI Architecture

The presentation layer is structured into focused user controls and dialogs in `View/`:
- **`KanbanBoardView.axaml` / `.cs`:**
  - Board selector dropdown with "+ New Board" and "Delete Board" actions.
  - Statistics KPI banner displaying Total, In Progress, Completed (%), Due Next 7 Days, and Overdue.
  - Horizontally scrollable column list with count badge, "+ Task" button, and cards.
  - **Native Drag-and-Drop:**
    - Dedicated grip drag handle (`⋮⋮`) with pointer movement threshold (>4px) preventing accidental drags when clicking Edit or Delete.
    - Card opacity dimming during drag operations.
    - Blue insertion line indicators showing exact drop position (before or after card).
    - Column border highlight on drag hover.
    - Support for moving between columns, reordering within the same column, and dropping onto empty columns or empty column space.
  - **Accessible Alternative:**
    - Directional move buttons (`◀` / `▶`) on each card for single-click keyboard and non-mouse navigation.
  - Task cards with priority pills, local due date indicators, strikethrough completed styling, edit, and delete buttons.
- **`GoalsView.axaml` / `.cs`:**
  - Goals card list with status badge (`Active`, `Completed`, `Cancelled`) and target date.
  - **Derived Progress Display:**
    - Visual progress bar and label (`X of Y tasks completed — Z%`).
    - Helpful `No tasks linked yet` empty state when no tasks are linked.
  - **Manage Tasks Button:** Opens `ManageGoalTasksDialog` to link/unlink tasks across boards.
  - Linked task chip list with strikethrough styling and checkmark for completed tasks.
- **`ManageGoalTasksDialog.axaml` / `.cs`:**
  - Modal displaying all tasks belonging to the user across all boards.
  - Shows task title, board name, column name, priority pill, and completion status badge.
  - Toggle action buttons: "+ Link" (primary blue) or "Unlink" (danger/ghost) with immediate reactive state update.
- **`HabitsView.axaml` / `.cs`:**
  - Active/paused habit list with streak counter pill.
  - 7-day completion grid with toggleable check controls (`✓` / `○`) based on `ILocalDateProvider`.
  - Habit pause/resume and deletion actions.
- **`GoalDialog.axaml` / `.cs`:**
  - Create and edit dialog with status dropdown options aligned to `GoalStatus`: `Active`, `Completed`, `Cancelled`.

---

## 5. Automated Verification

Comprehensive automated integration and unit tests are implemented in `Tests/`:
- `Tests/ProductivityServiceTests.cs` (29 baseline integration tests)
- `Tests/Phase1ProductivityTests.cs` (12 Phase 1 workflow and correctness tests)

**Total: 41 passing automated tests.**

### Test Coverage Summary:
1. `KanbanPositionHelper_ReorderWithinSameColumn_CalculatesAccurately`
2. `KanbanPositionHelper_CrossColumn_CalculatesAccurately`
3. `MoveTask_FullLifecycle_AndConsecutivePositions`
4. `MoveTask_CrossBoard_AndCrossUser_AreRejected`
5. `GoalService_LinkUnlink_AndDuplicateRejection`
6. `GetUserTasksAsync_RetrievesAcrossMultipleBoards_AndIsolatesUsers`
7. `DerivedGoalProgress_CalculatesCorrectly_AndRecalculatesOnColumnMove`
8. `SingleTask_CanBeLinkedToMultipleGoals`
9. `BoardStatistics_DeterministicLocalDates_AndSemantics`
10. `BoardStatistics_EmptyBoard_ReturnsZeroes`
11. `BoardStatistics_CustomAndMultipleCompletedColumns`
12. `BoardStatistics_IsStrictlyIsolatedPerBoard`

Run all tests:
```powershell
dotnet test Tests/Tests.csproj
```
