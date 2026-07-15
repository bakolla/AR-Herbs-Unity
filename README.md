Unity version: Unity 6: 6000.0.43f1

packages to install: 
AR Foundation
Google ARCore XR Plugin
Apple ARKit XR Plugin
Newtonsoft.Json

# AR Herb Unity Client & Backend

This repository contains the Unity AR Foundation client and the Node.js/Express proxy backend.

## Getting Started

### 1. Unity Client Setup
* Open this repository folder using **Unity Hub**.
* Ensure you are using **Unity 6 (6000.0.43f1)**.
* Check the scene `Assets/Scenes/MainARScene.unity` for the main setup.
* Set the backend URL in the `UIManager` inspector:
  * **In Unity Editor**: Use `http://localhost:3001` to connect to your local backend.
  * **On Mobile Devices**: Use your Pinggy, ngrok, or hosted HTTPS backend URL (e.g. `https://yourtunnel.pinggy.link`).

---

### 2. Backend Proxy Server Setup
The backend handles authentication and communication with PlantNet and Google Gemini APIs so that no private API keys are compiled into the Unity client binary.

1. Navigate to the `backend/` directory in your terminal:
   ```bash
   cd backend
   ```
2. Install dependencies:
   ```bash
   npm install
   ```
3. Create your local environment file:
   * Copy `backend/.env.example` to `backend/.env`.
4. Configure your API keys in `backend/.env`:
   * Add your **PlantNet** API key to `PLANTNET_API_KEY`.
   * Add your **Google Gemini** API key to `GEMINI_API_KEY`.
5. Start the development server:
   ```bash
   npm run dev
   ```
   The backend will start running locally on port **3001**.
