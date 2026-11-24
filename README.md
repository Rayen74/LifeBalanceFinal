# LifeBalance

LifeBalance is a modern, cross-platform personal productivity and financial wellness desktop application built with .NET 10, Avalonia UI, and PostgreSQL. It allows users to assess life fulfillment dimensions, track daily priorities, and manage personal accounts with secure local session persistence and user-isolated records.

---

## 1. Project Overview

The solution (`LifeBalanceF.sln`) is composed of four projects, each with a clear, single responsibility:

| Project | Target Framework | Responsibility |
|---|---|---|
| **Model** (`Model/Model.csproj`) | `net10.0` | Domain entities (`User`, `TaskBoard`, `TaskColumn`, `TaskItem`, `PersonalGoal`, `TaskGoal`, `Habit`, `HabitCompletion`), Entity Framework Core `LifeBalanceDbContext`, PostgreSQL mapping, and database migrations. |
| **Controller** (`Controller/Controller.csproj`) | `net10.0` | Authentication services (`AuthService`), Productivity services (`BoardService`, `GoalService`, `HabitService`), BCrypt password hashing (`PasswordService`), local JSON session manager (`SessionManager`), user repository, and dashboard state. |
| **View** (`View/View.csproj`) | `net10.0` | Avalonia desktop user interface, window orchestration, authentication forms (`LoginWindow`, `RegisterWindow`), main dashboard (`MainWindow`), Kanban board view (`KanbanBoardView`), habits view (`HabitsView`), goals view (`GoalsView`), and dialogs. |
| **Tests** (`Tests/Tests.csproj`) | `net10.0` | xUnit test suites (`AuthServiceTests`, `ProductivityServiceTests`) validating auth, sessions, Kanban workflows, goal links, streak calculations, and database constraints. |

---

## 2. Prerequisites

Before setting up the project, ensure you have the following installed on your machine:

1. **.NET SDK `10.0.404`**:
   The repository strictly pins the SDK version in `global.json`. You must install this exact version (not merely any .NET 10 SDK).
   Verify by running:
   ```powershell
   dotnet --version
   # Expected output: 10.0.404
   ```
2. **PostgreSQL Server (14+)**:
   Ensure PostgreSQL is installed and actively running on your system.
3. **Git**:
   For version control and repository synchronization.
4. **.NET CLI**:
   Included automatically with the .NET SDK.

---

## 3. Clone and Restore the Project

Follow these steps in order when setting up the repository for the first time:

### Step 1: Clone the repository
```powershell
git clone https://github.com/Rayen74/LifeBalanceFinal.git
cd LifeBalanceFinal
```

### Step 2: Verify the active .NET SDK
Check that the repository's pinned SDK is active:
```powershell
dotnet --version
```
If this outputs `10.0.404`, the SDK is properly configured. If not, install the matching SDK from Microsoft.

### Step 3: Restore local repository tools
Restore the pinned Entity Framework Core CLI tool (`dotnet-ef`):
```powershell
dotnet tool restore
```

### Step 4: Restore NuGet dependencies in locked mode
Restore all packages using the committed dependency lock files:
```powershell
dotnet restore --locked-mode
```
*Note: Using `--locked-mode` guarantees that NuGet resolves the exact dependencies specified in the committed lock files, preventing unexpected package drift or transitive version changes.*

### Step 5: Build the solution
Compile all projects without altering restored dependencies:
```powershell
dotnet build --no-restore
```

---

## 4. Reproducible Dependency Versioning System

LifeBalance enforces strict, reproducible dependency management across all development environments using the following configuration files:

| Configuration File | Responsibility |
|---|---|
| `global.json` | Pins the exact .NET SDK version (`10.0.404`) with `rollForward: "disable"` to eliminate SDK discrepancies between machines. |
| `Directory.Packages.props` | Implements **Central Package Management (CPM)**. Direct package versions are declared in this single file rather than scattered across individual `.csproj` files. |
| `Directory.Build.props` | Enables lock-file generation (`RestorePackagesWithLockFile = true`) for all projects across the solution. |
| `**/packages.lock.json` | Committed lock files (`Model`, `Controller`, `View`, `Tests`) that record the complete resolved dependency graph, including all transitive packages and hashes. |
| `.config/dotnet-tools.json` | Repository tool manifest pinning `dotnet-ef` to version `9.0.23`, ensuring identical CLI migration tooling across machines without requiring global tool installation. |

> **Important**: These configuration and lock files must always be committed to Git. Never delete or ignore `packages.lock.json`.

### Centrally Managed Packages

| Package | Version | Used In |
|---|---|---|
| `Avalonia` | `11.3.23` | View |
| `Avalonia.Desktop` | `11.3.23` | View |
| `Avalonia.Themes.Fluent` | `11.3.23` | View |
| `BCrypt.Net-Next` | `4.2.0` | Controller |
| `Microsoft.EntityFrameworkCore` | `9.0.23` | Model, Controller |
| `Microsoft.EntityFrameworkCore.Design` | `9.0.23` | Model, View |
| `Microsoft.EntityFrameworkCore.Relational` | `9.0.23` | Model |
| `Microsoft.NET.Test.Sdk` | `17.14.1` | Tests |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `9.0.3` | Model |
| `xunit` | `2.9.3` | Tests |
| `xunit.runner.visualstudio` | `3.1.0` | Tests |

### How to Intentionally Upgrade a Dependency

When upgrading or adding a package:
1. Update or add the `<PackageVersion>` entry in `Directory.Packages.props`.
2. Run a standard restore to regenerate the lock files:
   ```powershell
   dotnet restore
   ```
3. Compile the solution and execute the test suite:
   ```powershell
   dotnet build
   dotnet test
   ```
4. Review the diff in `packages.lock.json` to verify transitive dependency changes.
5. Commit `Directory.Packages.props` and the updated `packages.lock.json` files together.

---

## 5. PostgreSQL Database Configuration

The application communicates with PostgreSQL via Entity Framework Core and Npgsql.

### Default Connection String
If no environment variable is provided, `LifeBalanceDbContext` connects using:
```text
Host=localhost;Port=5432;Database=lifebalancedb;Username=postgres;Password=12345
```

### Overriding the Connection String
You can customize the connection string by setting the `LIFEBALANCE_DB` environment variable in your terminal:

**PowerShell (Windows):**
```powershell
$env:LIFEBALANCE_DB = "Host=localhost;Port=5432;Database=lifebalancedb;Username=your_user;Password=your_password"
```

**Bash / Linux / macOS:**
```bash
export LIFEBALANCE_DB="Host=localhost;Port=5432;Database=lifebalancedb;Username=your_user;Password=your_password"
```

> **Requirement**: PostgreSQL must be running and the target database (`lifebalancedb`) must already exist before applying migrations or launching the application. Entity Framework Core applies schema migrations; it does not create the PostgreSQL database server or create the database itself if your PostgreSQL user lacks creation privileges.

---

## 6. Database Migrations

Always use the locally restored `dotnet-ef` tool via `dotnet tool run dotnet-ef`.

### Applying Existing Migrations
To update an existing or freshly created `lifebalancedb` database with the committed schema migrations:
```powershell
dotnet tool run dotnet-ef database update --project Model/Model.csproj --startup-project View/View.csproj
```

*Note: The application also calls `Database.MigrateAsync()` during runtime startup when authentication operations execute. Applying migrations beforehand via CLI is recommended to verify schema status.*

### Creating a New Migration
When the data model in `Model/` is modified, generate a new migration:
```powershell
dotnet tool run dotnet-ef migrations add AddNewFeature --project Model/Model.csproj --startup-project View/View.csproj --output-dir Migrations
```
Then apply it to the database:
```powershell
dotnet tool run dotnet-ef database update --project Model/Model.csproj --startup-project View/View.csproj
```
Always review and commit the generated files inside `Model/Migrations/`.

---

## 7. Running the Application

After restoring dependencies, building the solution, and ensuring PostgreSQL is running:

```powershell
dotnet run --project View/View.csproj
```

* The desktop window will open with the **Login interface** if no previous local session exists.
* New users can click **Create an account** to register.
* If a valid session file (`session.json`) exists locally, LifeBalance automatically restores the session and opens the **Dashboard** directly.

---

## 8. Running Automated Tests

The test project (`Tests/Tests.csproj`) contains integration tests validating authentication logic, password hashing, session recovery, and PostgreSQL uniqueness constraints.

### Test Database Setup
Tests run against a dedicated test database named `lifebalancedb_test`. Ensure this database exists in your PostgreSQL instance:
```sql
CREATE DATABASE lifebalancedb_test;
```

To customize the test database connection string, set `LIFEBALANCE_TEST_DB`:
```powershell
$env:LIFEBALANCE_TEST_DB = "Host=localhost;Port=5432;Database=lifebalancedb_test;Username=your_user;Password=your_password"
```

### Executing the Tests
```powershell
dotnet test Tests/Tests.csproj
```

All 41 unit and integration tests execute against the test database and verify:
* User registration, UUID generation, and authentication.
* BCrypt hashing (no plaintext password storage).
* Case-insensitive and trimmed duplicate email rejection.
* Session creation, recovery across restarts, and invalidation on logout.
* Default column creation (`To Do`, `In Progress`, `Done`) and card positioning.
* Task moving across columns, position renumbering, and completion timestamp handling (`CompletedAt`).
* Drag-and-drop position calculation logic and reordering without off-by-one errors (`KanbanPositionHelper`).
* Strict multi-user data isolation across Kanban boards, tasks, habits, and goals.
* Deterministic dynamic board statistics (Total, In Progress, Completed, Due Next 7 Days, Overdue) with local calendar dates (`ILocalDateProvider`).
* Goal creation and task linking across user boards (`ManageGoalTasksDialog`).
* Derived goal progress calculation from linked task states.
* Habit creation, daily completion logging, uniqueness constraint enforcement, and consecutive streak calculations.
* Database constraint enforcement.

---

## 8.1. Productivity Module Architecture

For comprehensive documentation on the Productivity module design, entity models, database constraints, business workflows, and UI navigation, see:
👉 [docs/PRODUCTIVITY_MODULE.md](docs/PRODUCTIVITY_MODULE.md)

---

## 9. Troubleshooting

| Issue | Cause | Solution |
|---|---|---|
| `Cannot find SDK 10.0.404` | An incompatible or different .NET SDK is installed. | Install .NET SDK `10.0.404` as pinned in `global.json`, or verify with `dotnet --list-sdks`. |
| `Cannot find command 'dotnet-ef'` | Local repository tools were not restored. | Run `dotnet tool restore` in the repository root. |
| `The lock file is out of sync` | A dependency in a `.csproj` or `Directory.Packages.props` was changed without regenerating lock files. | Run `dotnet restore` (normal mode) to intentionally update the lock files, or revert the unauthorized package change. |
| `Npgsql.NpgsqlException: Connection refused` | PostgreSQL service is stopped or port `5432` is unreachable. | Start your PostgreSQL server service (`net start postgresql-x64-XX` or via Services app). |
| `3D000: database "lifebalancedb" does not exist` | The database has not been created yet in PostgreSQL. | Connect to PostgreSQL via `psql` or pgAdmin and run `CREATE DATABASE lifebalancedb;`. |
| `Build failed because dependencies not restored` | A build was executed before restoring packages. | Run `dotnet restore --locked-mode` followed by `dotnet build --no-restore`. |
