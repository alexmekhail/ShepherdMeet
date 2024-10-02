# ShepherdMeet

**ShepherdMeet** is a web-based platform designed to facilitate scheduling, organizing, and managing meetings for church leaders, members, or other community-based groups. The application streamlines communication and offers a centralized hub for event management.

## Table of Contents

- [Project Overview](#project-overview)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Installation](#installation)
- [Usage](#usage)

---

## Project Overview

ShepherdMeet aims to provide an intuitive solution for church leaders and community organizers to coordinate meetings. With easy-to-use scheduling and notification features, ShepherdMeet simplifies event planning while fostering better communication within groups.

### Purpose

This platform addresses the common challenges faced by church leaders when coordinating meetings, events, and communication with their congregation or team. ShepherdMeet serves as a single interface to set up meetings, notify participants, and manage schedules.

## Features

- **Meeting Scheduling**: Set up and manage meetings with details such as date, time, and agenda.
- **Admin Interface**: Provide an admin panel for leaders to manage users and events.
- **User-Friendly Interface**: Simple and accessible UI for non-technical users.

## Tech Stack

- **Front-end**: React.js, HTML, CSS
- **Back-end**: ASP.NET, C#
- **Database**: SQLite
- **Authentication**: JWT (JSON Web Token) for secure user sessions

## Installation

### Prerequisites

Ensure that you have the following installed:
- Node.js
- npm (or Yarn)
- MongoDB (or MongoDB Atlas)

### Steps to Set Up Locally

1. **Clone the repository:**
   ```bash
   git clone https://github.com/alexmekhail/ShepherdMeet.git
   cd ShepherdMeet
   ```

2. **Install dependencies:**
   ```bash
   npm install
   ```

3. **Set up environment variables:**

   Create a `.env` file in the root of the project and configure the following variables:

   ```bash
   MONGO_URI=your_mongo_uri
   JWT_SECRET=your_secret_key
   PORT=5000
   ```

4. **Run the application:**
   ```bash
   npm start
   ```

5. **Access the app in the browser:**
   Open `http://localhost:5000` in your browser.

## Usage

Once the application is running, you can create meetings, invite attendees, and manage events from the admin interface. The platform is designed to allow users to receive notifications via email or SMS when meetings are scheduled or modified.
