# LifeBalance

LifeBalance is a .NET desktop application built with Avalonia UI and PostgreSQL. The current version includes account registration/login and a sample life-balance dashboard.

## Project structure

The solution contains three application projects and one test project:

- **Model** – user entity, Entity Framework Core `DbContext`, PostgreSQL configuration, and migrations.
- **Controller** – authentication services, BCrypt password hashing, session management, repository, and dashboard logic.
- **View** – Avalonia desktop UI, application startup, login/register screens, and dashboard.
- **Tests** – xUnit tests for authentication and persistence.

## Technologies and package versions

All projects target **.NET 10.0** (`net10.0`). Package versions currently specified in the project files:

| Project | Package | Version |
|---|---|---:|
| Model | Microsoft.EntityFrameworkCore | 9.0.23 |
| Model | Microsoft.EntityFrameworkCore.Design | 9.0.23 |
| Model | Microsoft.EntityFrameworkCore.Relational | 9.0.23 |
| Model | Npgsql.EntityFrameworkCore.PostgreSQL | 9.0.3 |
| Controller | BCrypt.Net-Next | 4.2.0 |
| Controller | Microsoft.EntityFrameworkCore | 9.0.23 |
| View | Avalonia | 11.3.* |
| View | Avalonia.Desktop | 11.3.* |
| View | Avalonia.Themes.Fluent | 11.3.* |
| View | Microsoft.EntityFrameworkCore.Design | 9.0.23 |
| Tests | Microsoft.NET.Test.Sdk | 17.14.1 |
| Tests | xunit | 2.9.3 |
| Tests | xunit.runner.visualstudio | 3.1.0 |

`11.3.*` is a floating version range declared in `View/View.csproj`; NuGet resolves a matching 11.3 release during restore. The package references are defined in each project's `.csproj` file.

## Database configuration

The application uses PostgreSQL through Entity Framework Core and the Npgsql provider.

Default connection:

```text
Host=localhost;Port=5432;Database=lifebalancedb;Username=postgres;Password=12345
```

You can override it with the `LIFEBALANCE_DB` environment variable.

**Before running the app:** make sure PostgreSQL is installed and running. Create the `lifebalancedb` database, or use a connection string pointing to a database your PostgreSQL user can access.

### Run database migrations

Install the EF Core CLI tool if it is not already installed:

```bash
dotnet tool install --global dotnet-ef --version 9.0.23
```

From the solution root, apply all pending migrations:

```bash
dotnet ef database update --project Model/Model.csproj --startup-project View/View.csproj
```

To create a new migration after changing the model:

```bash
dotnet ef migrations add MigrationName --project Model/Model.csproj --startup-project View/View.csproj
```

Then apply it:

```bash
dotnet ef database update --project Model/Model.csproj --startup-project View/View.csproj
```

The application also calls `Database.MigrateAsync()` when authentication first needs the database, so pending migrations are applied at runtime. Running the CLI command manually is useful to apply/check migrations before launching the UI.

## Build and run

Run these commands from the repository root:

```bash
dotnet restore LifeBalanceF.sln
dotnet build LifeBalanceF.sln
dotnet run --project View/View.csproj
```

The login window is shown first when there is no valid saved session. From there, you can sign in or create an account.

## Run tests

The tests use a separate PostgreSQL database named `lifebalancedb_test` by default. Make sure it exists and PostgreSQL is running, then run:

```bash
dotnet test Tests/Tests.csproj
```

You can override the test connection string with `LIFEBALANCE_TEST_DB`. Do not point the test connection string at your normal application database.
