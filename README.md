# 🌟 Dynamic Job Runner App 🚀

A web application built with **ASP.NET Core** designed to schedule, execute, and manage dynamic jobs with real-time interruption support.

---

## 📝 Description

**Dynamic Job Runner App** enables users to schedule and manage dynamic job executions using Cron expressions. It includes advanced features like real-time job interruption and is designed for tasks requiring concurrent processing or scheduled intervals, such as:

- Sending emails.
- Running automated scripts.
- Managing background operations.

---

## ✨ Key Features

- 🕒 **Cron-based scheduling** for flexible task execution timing.
- 🛠️ **Real-time job interruption** via **Quartz.NET** for dynamic control.
- 📊 **Execution history tracking** to monitor past and current tasks.
- ⭐ **Easy configuration and extensibility** to adapt to your use case.
- 🎯 **Clean and modular architecture** with support for **Entity Framework Core**.

---

## 🧰 Tech Stack

- **ASP.NET Core 8.0** – Framework for building the web application.
- **Quartz.NET** – Job scheduling and execution engine.
- **PostgreSQL** – Database for task storage and retrieval.
- **Entity Framework Core** – ORM for data access and modeling.
- **Razor Pages & Bootstrap** – For building a responsive and polished UI.

---

## 🚀 Getting Started

Follow these steps to get the project up and running quickly using Docker.

### 1️⃣ Clone the repository:

```bash
git clone https://github.com/enzoaf99/dynamic-job-runner.git
```

### 2️⃣ Build and run the application with Docker Compose:

```bash
docker-compose up --build
```

This will:
- Build the application image using the provided `Dockerfile`.
- Start the application and PostgreSQL database services in connected containers.

By default, the app will be accessible at:  
[http://localhost:8080](http://localhost:8080).

### 3️⃣ Shut down the application (when needed):

You can stop the containers and remove associated resources by running:

```bash
docker-compose down
```

---

## 📋 Usage

### **Create a New Job**

1. Go to the dashboard, enter the job details:
- A name for the job.
- A valid Cron expression for scheduling.
- The script or command to execute (e.g., a `curl` command).
2. Enable or disable the job as needed.

### **Manage Existing Jobs**

From the dashboard, you can:

- ✅ Pause/unpause jobs.
- ✏️ Edit job configurations.
- 🗑️ Delete unwanted scheduled jobs.

### **Monitor Executions**

- 📊 Review the **execution history** to inspect past results.
- ❌ Cancel running jobs in real time as necessary.

---

## 🔧 Configuration

### **Database Connection**

The app uses a PostgreSQL database, and the connection string has been preconfigured in the `docker-compose.yml` file to connect to the `db` service. Here are the relevant environment variables:

```yaml
environment:
  POSTGRES_USER: postgres
  POSTGRES_PASSWORD: yourpass
  POSTGRES_DB: jobrunner
```

You can modify these values as needed in the `docker-compose.yml` file.

### **Environment Variables**

The application relies on the following environment variables for customization:

- `POSTGRES_USER`: PostgreSQL database username (default: `postgres`).
- `POSTGRES_PASSWORD`: PostgreSQL database password (default: `yourpass`).
- `POSTGRES_DB`: PostgreSQL database name (default: `jobrunner`).
- `ASPNETCORE_ENVIRONMENT`: Specifies the environment for the app (default: `Development`).
- `ASPNETCORE_URLS`: URL bindings for the app (default: `http://+:80`).
- `ConnectionStrings__Default`: Connection string for the database.

Additionally, two new environment variables are available for basic authentication:

- `AUTH_USERNAME`: Username for basic authentication (default: `admin`).
- `AUTH_PASSWORD`: Password for basic authentication (default: `securepassword`).

These are configured in the `docker-compose.yml` file and can be customized as needed.

---

## 🤝 Contributions

Contributions are welcome! Follow these steps to collaborate:

1. Fork the project.
2. Create a new branch for your contribution:

   ```bash
   git checkout -b feature/your-feature
   ```

3. Make your changes and commit:

   ```bash
   git commit -m "Add your feature"
   ```

4. Push your changes and open a Pull Request:

   ```bash
   git push origin feature/your-feature
   ```

---

## 📜 License

This project is licensed under the **MIT License**. You can use, modify, and distribute the code freely as long as the original license notice is included.

---

## 📬 Contact

If you have any questions or suggestions, feel free to reach out:

- 🌐 **GitHub:** [@enzoaf99](https://github.com/enzoaf99)
- 📧 **Email:** enzoafernandez99@gmail.com