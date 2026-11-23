# LifeBalance — Productivity Module Specification & Architecture

This document describes the design, architecture, database schema, business rules, and UI integration of the **Productivity Module** in LifeBalance.

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
- **Positioning:** Tasks and columns use zero-based consecutive integers (`0, 1, 2...`). Insertion appends or inserts at specific index, with adjacent positions re-indexed inside a database transaction.
- **Move / Reorder:** Moving a task to a column where `is_completed = true` stamps `CompletedAt = DateTime.UtcNow`. Moving a task back to an active column clears `CompletedAt = null`.
- **Column Deletion Protection:** A column cannot be deleted if it is the only column on a board. If tasks exist, they must be reassigned to a designated target column before removal.
- **Statistics Calculation:** `GetBoardStatisticsAsync` computes dynamic metrics on the fly without database locks or computed columns:
  - `TotalTasks`: count of all tasks across board columns.
  - `InProgressTasks`: tasks in non-completed columns with `position > 0` (excluding initial To Do column).
  - `CompletedTasks`: tasks in columns marked `is_completed = true`.
  - `CompletionPercentage`: `(completed / total) * 100` rounded to 1 decimal place.
  - `DueInNext7Days`: non-completed tasks with `DueDate` between today and today + 7 days.
  - `OverdueTasks`: non-completed tasks with `DueDate < today`.

### Personal Goals (`GoalService.cs`)
- **Goal Management:** CRUD for goals with `GoalStatus` (`Active`, `Completed`, `Cancelled`).
- **Task Association:** Tasks can be linked to multiple goals (`TaskGoal`).
- **Strict User Isolation:** The service explicitly verifies that both the `TaskItem` and the `PersonalGoal` belong to the same authenticated `UserId`. Cross-user linking requests are rejected. Duplicate links are safely handled.

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
- **`KanbanBoardView.axaml`:**
  - Board selector dropdown with "+ New Board" and "Delete Board" actions.
  - Statistics KPI banner displaying Total, In Progress, Completed (%), Due Soon, and Overdue.
  - Horizontally scrollable column list with count badge, "+ Task" button, and cards.
  - Task cards with priority pills, due date indicators, strikethrough completed styling, and directional move (`◀` / `▶`), edit, and delete buttons.
- **`HabitsView.axaml`:**
  - Active/paused habit list with streak counter pill.
  - 7-day completion grid with toggleable check controls (`✓` / `○`).
  - Habit pause/resume and deletion actions.
- **`GoalsView.axaml`:**
  - Goals card list with status badge (`Active`, `Completed`, `Cancelled`) and target date.
  - Linked task chip list showing associated Kanban items.
- **`MainWindow.axaml`:**
  - Unified sidebar navigation:
    1. **Dashboard** (Life Dimensions score and balance overview)
    2. **Tasks & Kanban** (`KanbanBoardView`)
    3. **Habits & Streaks** (`HabitsView`)
    4. **Personal Goals** (`GoalsView`)

---

## 5. Automated Verification

Comprehensive automated integration and business tests are implemented in `Tests/ProductivityServiceTests.cs`:
1. `CreateBoard_GeneratesDefaultColumns_AndAssociatesToUser`
2. `MoveTask_UpdatesColumnAndPositions_AndSetsCompletedAtCorrectly`
3. `CrossUser_TaskAccess_IsStrictlyPrevented`
4. `GetBoardStatistics_CalculatesAccurateMetrics`
5. `GoalLinking_AssociatesTaskAndRejectsCrossUserLinks`
6. `HabitService_RecordsCompletions_AndEnforcesUniqueness`
7. `HabitService_CalculatesCurrentStreak_Accurately`

Run all tests:
```bash
dotnet test Tests/Tests.csproj
```
