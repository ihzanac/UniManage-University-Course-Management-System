# 🎓 UniManage – University Course Management System

## 💻 Web-Based University Course Management System

UniManage – University Course Management System (UCMS) is a web-based application designed to streamline and automate academic and administrative activities within a university environment.

The system provides a centralized platform for students, lecturers, and administrators to manage courses, enrollments, assignments, grades, payments, communication, and academic reports.

---

## 🌍 Project Overview

The UniManage system is developed to make university course management easier, faster, and more organized.

The system provides different features for:

- 👨‍🎓 Students
- 👨‍🏫 Lecturers
- 👨‍💼 Administrators

The platform uses role-based access control to provide users with features relevant to their responsibilities.

---

## ✨ Main Features

### 👨‍🎓 Student Features

- 📝 Student Registration
- 🔐 Student Login
- 🔑 Secure Authentication
- 📊 Student Dashboard
- 📚 Browse Available Courses
- 🔎 Search and Filter Courses
- 📝 Course Enrollment
- 💳 Course Payment
- 📜 View Payment History
- 📋 View Assignment Details
- 📤 Submit Assignments
- 🎓 View Grades
- 💬 View Lecturer Feedback
- 💌 Student Messaging
- 👤 Manage Profile

---

### 👨‍🏫 Lecturer Features

- 🔐 Lecturer Login
- 📊 Lecturer Dashboard
- 📚 Course Management
- 📝 Manage Course Activities
- 📁 Upload Course Materials
- 📋 Create Assignments
- 📝 Manage Assignments
- ⏰ Set Assignment Deadlines
- 👀 View Student Submissions
- 🎓 Grade Assignments
- 💬 Provide Student Feedback
- 📊 Generate Course Reports
- 💌 Student Communication
- 💬 Lecturer Messaging

---

### 👨‍💼 Admin Features

- 🔐 Admin Login
- 📊 Admin Dashboard
- 👥 User Management
- 📚 Course Management
- 🗂️ Course Category Management
- 📝 Enrollment Management
- 💳 Payment Management
- 👨‍🎓 Manage Students
- 👨‍🏫 Manage Lecturers
- 📊 Generate Reports
- 📈 Enrollment Statistics
- 📊 System Usage Monitoring
- 👀 User Activity Monitoring
- ⚙️ System Settings
- 🔒 Role-Based Access Control

---

## 📚 Course Management

The course management module allows administrators to:

- ➕ Create Courses
- ✏️ Update Course Details
- 🗑️ Delete Courses
- 🗂️ Manage Course Categories
- 👨‍🏫 Assign Lecturers
- 📚 Manage Course Availability
- 📊 Monitor Course Enrollments

Students can browse available courses and enroll in selected courses.

---

## 📝 Assignment Management

The assignment management module supports the academic assignment workflow.

### 👨‍🏫 Lecturers Can

- ➕ Create Assignments
- ⏰ Set Assignment Deadlines
- 📁 Upload Course Materials
- 👀 View Student Submissions
- 📝 Evaluate Assignments
- 🎓 Enter Grades
- 💬 Provide Feedback

### 👨‍🎓 Students Can

- 👀 View Assignments
- ⏰ Check Deadlines
- 📤 Submit Assignments
- 🎓 View Grades
- 💬 View Lecturer Feedback

---

## 💳 Payment Management

The system provides payment-related functionality for course enrollment.

- 💰 Course Fee Management
- 💳 Payment Processing
- ✅ Payment Verification
- 📜 Payment Records
- 🧾 Payment History
- 🧾 Payment Receipts

---

## 💬 Communication Module

UniManage includes a messaging module to improve communication between students and lecturers.

Users can:

- 📤 Send Messages
- 📥 Receive Messages
- 💬 View Conversations
- 🔔 Receive Message Notifications

---

## 📊 Reports and Analytics

The system provides reporting and monitoring features for administrators and lecturers.

Reports can include:

- 📈 Student Enrollment Statistics
- 📚 Course Information
- 🎓 Student Performance
- 👨‍🏫 Lecturer Workload
- 💳 Payment Information
- 📊 System Usage
- 👥 User Activity

---

## 🛠️ Technologies Used

### 🎨 Frontend Technologies

- 🌐 HTML5
- 🎨 CSS3
- ⚡ JavaScript
- 🅱️ Bootstrap 5

### ⚙️ Backend Technologies

- 🟣 ASP.NET MVC 5
- 🔷 C#

### 🗄️ Database

- 🟦 Microsoft SQL Server

### 🧰 Development Tools

- 🟪 Visual Studio 2022
- 🗄️ SQL Server Management Studio
- 🐙 Git
- 🐙 GitHub

---

## 🏗️ System Architecture

UniManage follows a 3-Tier Architecture.

```text
                    ┌─────────────────────┐
                    │        👤 User      │
                    │ Student / Lecturer  │
                    │       / Admin       │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │ 🎨 Presentation Tier│
                    │   HTML / CSS / JS   │
                    │     Bootstrap       │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │ ⚙️ Business Logic   │
                    │    ASP.NET MVC      │
                    │         C#          │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │ 🗄️ Data Tier        │
                    │    SQL Server       │
                    └─────────────────────┘

📂 Project Structure

UniManage
│
├── 📁 Admin
│   ├── 📊 Dashboard
│   ├── 👥 Users
│   ├── 📚 Courses
│   ├── 🗂️ Categories
│   ├── 💳 Payments
│   ├── 📊 Reports
│   └── 📈 System Usage
│
├── 📁 Student
│   ├── 🔐 Login
│   ├── 📝 Registration
│   ├── 📊 Dashboard
│   ├── 📚 Courses
│   ├── 📝 Assignments
│   ├── 🎓 Grades
│   ├── 💳 Payments
│   └── 💬 Messages
│
├── 📁 Lecturer
│   ├── 🔐 Login
│   ├── 📊 Dashboard
│   ├── 📚 Courses
│   ├── 📝 Assignments
│   ├── 🎓 Grading
│   ├── 📊 Reports
│   └── 💬 Messages
│
├── 📁 Models
│
├── 📁 Controllers
│
├── 📁 Views
│
├── 📁 Scripts
│
├── 📁 Content
│
├── 📁 App_Start
│
├── ⚙️ Web.config
│
└── 📦 packages.config

🗄️ Database

UniManage uses Microsoft SQL Server as the database management system.

The database manages information related to:

👥 Users
🔐 User Roles
📚 Courses
🗂️ Course Categories
📝 Enrollments
📋 Assignments
📤 Assignment Submissions
🎓 Grades
💳 Payments
💬 Messages
🔔 Message Notifications
📩 Contact Messages
📊 System Usage Logs
🔐 Security

The system includes security features to protect user and academic information.

Security features include:

🔐 User Authentication
👥 Role-Based Access Control
✅ Input Validation
🔑 Secure Login
🛡️ User Authorization
🔒 Password Protection
🗄️ Data Protection
📊 System Activity Logging
🛡️ Database Security
💾 Backup Support
👥 User Roles
👨‍🎓 Student

Students can:

📝 Register
🔐 Login
📚 Browse Courses
📝 Enroll in Courses
💳 Make Payments
📤 Submit Assignments
🎓 View Grades
💬 View Feedback
💌 Communicate with Lecturers
👨‍🏫 Lecturer

Lecturers can:

🔐 Login
📚 Manage Courses
📁 Upload Materials
📝 Create Assignments
📤 Manage Submissions
🎓 Enter Grades
💬 Provide Feedback
📊 Generate Reports
💌 Communicate with Students
👨‍💼 Administrator

Administrators can:

👥 Manage Users
📚 Manage Courses
🗂️ Manage Categories
📝 Manage Enrollments
💳 Manage Payments
📊 Generate Reports
📈 Monitor System Usage
⚙️ Manage System Settings
