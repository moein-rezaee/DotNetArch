# **DotNetArch: Project Scaffold CLI Tool**  
**A .NET CLI tool for automating the creation of Clean Architecture projects and layers.**  

### **Features**  
- Automatically creates a solution (`.sln`) with Clean Architecture layers:
  - Core  
  - Application  
  - Infrastructure  
  - API  
- Sets up the proper project references between layers.  
- Ensures a consistent project structure.  

---

### **Installation**  
#### **Pre-requisites:**  
1. [.NET SDK](https://dotnet.microsoft.com/download) version 9.0 or higher.  
2. A terminal or command-line interface (e.g., Command Prompt, PowerShell).  

#### **Clone the Repository**  
To get started, clone this repository:  
```bash
git clone https://github.com/moein-rezaee/DotNetArch.git
cd DotNetArch
```

#### **Build the Tool**  
Run the following command to build the project:  
```bash
dotnet build
```

---

### **Usage**  
1. Run the CLI tool:  
   ```bash
   dotnet run
   ```  

2. Enter the desired name for your project when prompted.  

3. The tool will generate the following structure:  
   ```
   MyProject.sln
   ├── MyProject.Core
   ├── MyProject.Application
   ├── MyProject.Infrastructure
   └── MyProject.API
   ```

4. Layer references will be set up as follows:  
   - `Application` → `Core`  
   - `Infrastructure` → `Application`  
   - `API` → `Application` + `Infrastructure`  

---

### **Future Enhancements**  
- Add NuGet packages like `MediatR`, `FluentValidation`, etc.  
- Support for other architectural styles such as Onion, Hexagonal, or Microservices.  
- Template customization for adding pre-configured files and configurations.  

---

### **Contributing**  
We welcome contributions! Please follow the steps below to contribute:  
1. Fork the repository.  
2. Create a feature branch:  
   ```bash
   git checkout -b feature/your-feature-name
   ```  
3. Commit your changes:  
   ```bash
   git commit -m "Add your message here"
   ```  
4. Push to your branch:  
   ```bash
   git push origin feature/your-feature-name
   ```  
5. Open a Pull Request.  

---

### **License**  
This project is licensed under the [MIT License](LICENSE). Feel free to use and distribute it as you like.  

---

### **Contact**  
For questions or feedback, please contact me at: [me.rezaei1996@gmail.com](mailto:me.rezaei1996@gmail.com).  

---