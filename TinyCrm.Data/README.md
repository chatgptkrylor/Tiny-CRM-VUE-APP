# TinyCrm.Data - EF6 middle tier

Entity Framework 6 data-access middle tier for TinyCrm. Connects the
ASP.NET MVC 5 web application to SQL Server via an EDMX model-first
data model.

## Layout

- `TinyCrmModel.edmx` - the model-first EDMX (conceptual model, storage
  model, mappings in one file).
- `TinyCrmModel.csdl` / `.ssdl` / `.msl` - the three EDMX runtime
  sections, embedded in the assembly as manifest resources. The EF
  connection string references them via `res://TinyCrm.Data/TinyCrmModel.*`.
- `extract-edmx.ps1` - splits `TinyCrmModel.edmx` into the three files
  above. **Run it after editing the EDMX** to keep the embedded
  artifacts in sync.
- `TinyCrmModel.sql` - database DDL (equivalent of the designer's
  "Generate Database from Model"). The app usually creates the database
  automatically via the EF `CreateDatabaseIfNotExists` initializer; the
  script is for manual provisioning.
- `TinyCrmModel.Context.cs` - the `TinyCrmEntities` DbContext.
- `Models/` - POCO entity classes (`Customer`, `Interaction`, `User`,
  enums). These keep their DataAnnotations, so MVC view validation is
  unchanged.
- `Repositories/` - `CustomerRepository`, `InteractionRepository`,
  `UserRepository` (the middle tier API used by the web controllers).
- `DatabaseSeeder`, `DatabaseSetup`, `TinyCrmDatabaseInitializer` -
  database creation and seeding (admin/admin123, demo/demo123,
  5 customers, 6 interactions).
- `DbContextFactory` - central factory for short-lived DbContexts;
  tests override it to use a dedicated test database.

## Editing the model

On a machine with Visual Studio and the Entity Framework designer:

1. Open `TinyCrmModel.edmx` in the designer and edit it.
2. Run `powershell -File extract-edmx.ps1` in this folder to
   regenerate the embedded CSDL/SSDL/MSL artifacts.
3. Review `TinyCrmModel.sql` if you provision databases manually.

## Connection string

The default context constructor resolves `name=TinyCrmEntities` from
the app configuration. In the web project it targets SQL Server
LocalDB:

```
data source=(localdb)\MSSQLLocalDB;initial catalog=TinyCrm;integrated security=True
```

To target a different SQL Server, change the `provider connection
string` inside the `TinyCrmEntities` entry in `TinyCrm/Web.config`.
