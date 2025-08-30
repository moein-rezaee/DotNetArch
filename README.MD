<img src="assets/icon.png" width="128" height="128" style="vertical-align: middle;"/>

# **DotNetArch: Simplify Clean Architecture Setup**  

#### **Introduction**  

DotNetArch is a .NET global tool designed to help developers quickly set up solutions following Clean Architecture principles and Domain-Driven Design (DDD). This tool streamlines the creation of well-structured .NET projects, saving time and effort while adhering to industry standards.  

---

#### **Requirements**  

Before using DotNetArch, ensure your system meets the following requirements:  

1. **.NET SDK**: Version 6.0 or higher (recommended: .NET 9.0).  
   - Install it from the official [.NET Downloads page](https://dotnet.microsoft.com/download).  
2. **Operating System**:  
   - **Windows**: Windows 10 or higher.  
   - **macOS**: macOS Catalina or higher.  
   - **Linux**: Any distribution supporting .NET Core.  
3. **Git**: Ensure Git is installed if you plan to clone or contribute.  

To check your .NET version, run:  

```bash
dotnet --version
```  

---

#### **How to Install and Use the NuGet Package**  

**Step 1: Install the Tool**  
You can install DotNetArch globally via NuGet with the following command:  

```bash
dotnet tool install --global DotNetArch
```  

**Step 2: Update the Tool**  
To ensure you have the latest version of the tool:  

```bash
dotnet tool update --global DotNetArch
```  

**Step 3: Generate a Clean Architecture Solution**  
Once installed, you can use the tool to create a solution structure:  

```bash
dotnet arch
```

This command generates the following layers:

- **Core**
- **Application**
- **Infrastructure**
- **API**

#### Generate CRUD for an Entity

After creating a solution you can scaffold a full CRUD vertical slice for an entity:

```bash
dotnet-arch new crud <SolutionName> <EntityName>
```

The tool creates domain models, repositories, unit of work, MediatR command/query handlers, validators, and API controllers. You can choose SQL Server or SQLite and generated code supports pagination via `PagedResult<T>`. Running the command again updates existing files instead of duplicating them.

---

#### **How to Clone and Run the Project**

**Step 1: Clone the Repository**  

To get the source code, clone the repository from GitHub:  

```bash
git clone https://github.com/moein-rezaee/DotNetArch.git
cd DotNetArch
```  

**Step 2: Build the Project**  

Ensure you have the .NET SDK installed. Then, build the project:  

```bash
dotnet build
```  

**Step 3: Run the Project**  

To test the functionality, you can execute the project locally:  

```bash
dotnet run
```  

---

#### **How to Contribute**  

We welcome contributions to DotNetArch! Here’s how you can contribute:  

1. **Fork the Repository**:  
   Create a fork of the main repository to your GitHub account.  

2. **Create a New Branch**:  
   Use a meaningful branch name, such as `feature/add-new-command`.  

   ```bash
   git checkout -b feature/add-new-command
   ```  

3. **Make Your Changes**:  
   Ensure your code adheres to .NET coding guidelines.  

4. **Push and Submit a Pull Request**:  
   Push your changes and open a pull request to the main repository.  

   ```bash
   git push origin feature/add-new-command
   ```  

---

#### **Features**

- **Automated Project Setup**: Generate Clean Architecture solutions with a single command.
- **Cross-Platform**: Fully functional on Windows, macOS, and Linux.
- **Domain-Driven Design Support**: Includes pre-configured layers for DDD.
- **Ease of Use**: Intuitive and straightforward CLI commands.
- **`new crud` scaffolding**: Generates vertical-slice CQRS CRUD with MediatR, FluentValidation, EF Core repositories, and paginated results.
- **Database Provider Selection**: Choose between SQL Server and SQLite during scaffolding.
- **Idempotent Generation**: Re-running commands updates existing files without creating duplicates.
- **Repository & Unit of Work patterns**: Each entity gets its own repository and UoW registration for clean data access.

---

#### **Future Goals**  

- Add support for other architectural patterns such as **Microservices** and **Event-Driven Design**.  
- Improve customization options for solution templates.  
- Add integration with popular libraries and frameworks like **Fluent Validation**, **MediatR**, and **MassTransit**.  
- Provide GUI support for non-CLI users.  

---

#### **Contact Me**  

If you have any questions, suggestions, or issues, feel free to reach out:  

- **GitHub Issues**: [Report Here](https://github.com/moein-rezaee/DotNetArch/issues)  
- **Email**: me.rezaei1996@gmail.com  
- **LinkedIn**: [Moein Rezaee](https://linkedin.com/in/moein-rezaee-26331a125)  

---

Start simplifying your .NET project setup today with **DotNetArch**! 🚀