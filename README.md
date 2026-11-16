# C# BaseCode
## _ASI Bridge/JumpStart Program BaseCode_

This is the base code that will be used during the bridge and jumpstart program.
```
```
## Installation
1.  Open the `.sln` file.
    _※Use Visual Studio 2022_
2.  Change connection strings on the `appsettings.json` base on your server
3.  Set `ASI.Basecode.WebApp` as the Startup project.
4.  Clean and ReBuild the solution.

## Demo Accounts
Borrower	maria.santos@university.edu.ph	Campus123

Custodian	carlos.diaz@university.edu.ph	Campus123

Administrator	ana.reyes@university.edu.ph	Campus123

## Code Structure

- `ASI.Basecode.Data`
    - This project contains the repositories and other logics that involves with database.
    - Make sure that the repository is for db processing only.
    - If it involves additional logic, move the logic into the service file.
    - Folders: `Interfaces/` (repository interfaces), `Repositories/`, `Models/` (entities), `QueryModels/` (paged/aggregate query results).
- `ASI.Basecode.Resources`
    - This project is dedicated to storing messages, labels, and other resources used by the website.
    - It serves as a centralized location for managing static content that is displayed to users.
    - Store all messages, labels, translations, and any non-code assets required for the site in this project.
    - `Constants/` holds one class per file for role and status codes; use them instead of string literals.
- `ASI.Basecode.Services`
    - This project contains the services and other processing logics before connecting to the repository.
    - This can also contains classes that can be use by the WebApp project with/without db processing.
    - Folders: `Services/`, `Interfaces/`, `ViewModelClasses/`, `Manager/` (domain rules/helpers), `ServiceModels/`, `Mapping/` (AutoMapper profile), `Seeding/` (database seeder).
- `ASI.Basecode.WebApp`
    - This project contains the main codes especially the `Controllers` and `Views`.
    - This is where you put the connection logic to the APIs.
    - Make sure that the controllers are clean and no other logic should involve.
    - Folders: `Mvc/` (controller base, auth cookie, filters), `HostedServices/`, `Infrastructure/`, `Helpers/` (view helpers), `Configuration/`. Inspection photos are stored privately in `App_Data/uploads`.
    - If it involves additional logic, move the logic into the service file.
```
```
