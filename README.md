# AR Herb - Android Mobile Setup & Build Guide

This document explains how to set up, build, and run the AR Herb client application on an Android mobile device, alongside local testing fallback in the Unity Editor.

---

## 1. Unity Project & XR Configuration (Android)

To enable AR capabilities on Android, make sure the following packages are installed and configured:

1. **Install Required Packages**:
   - Open **Window > Package Manager** in Unity.
   - Install **AR Foundation** (under the Unity Registry tab).
   - Install **Google ARCore XR Plugin**.
   - Install **XR Plugin Management**.

2. **Configure XR Settings**:
   - Go to **Edit > Project Settings > XR Plug-in Management**.
   - Under the **Android** tab (indicated by the Android robot icon), check the box for **Google ARCore** to enable the ARCore subsystem.

---

## 2. Generating the Main Scene

1. Open the project in the Unity Editor.
2. In the top toolbar, click **AR Herb > Build MVP Scene**.
3. This generates the scene at `Assets/Scenes/MainARScene.unity`.
4. **Editor Fallback & No Warnings**: 
   - When generating the scene, the `ARMobileRoot` (which houses AR Session and XR Origin) is automatically configured as **Inactive** by default in the Editor.
   - When you press **Play** in Unity Editor, the client runs using `EditorWebcamCaptureProvider` (podgląd z kamerki internetowej lub obraz testowy) and prints `"Editor mode: using webcam fallback"`. 
   - This ensures you get **zero** XR subsystem warnings or tracked pose driver complaints while developing.

---

## 3. Android Build & Deployment

To build the APK and deploy to your Android phone:

1. **Install Android Support in Unity Hub**:
   - Ensure the Unity installation you are using has **Android Build Support**, **Android SDK & NDK Tools**, and **OpenJDK** modules installed.

2. **Switch Platform**:
   - In Unity, go to **File > Build Settings**.
   - Select **Android** and click **Switch Platform**.

3. **Configure Player Settings**:
   - Click **Player Settings...** in the bottom left of the Build Settings window.
   - Under **Player > Other Settings**:
     - **Package Name**: Set this to `com.bakolla.arherb`.
     - **Minimum API Level**: Set to Android 7.0 (API Level 24) or higher (required for ARCore).
     - **Camera Usage Description**: Ensure a valid description is set (e.g. "Wymagane uprawnienie do aparatu w celu identyfikacji okazów roślin.").
     - **Internet Access**: Set to **Require** to enable backend communication.

4. **Requesting Camera Permission**:
   - The app dynamically requests the Android Camera permission on startup.
   - If not yet granted, it displays the status message `"Waiting for camera permission..."` and waits until permissions are authorized before starting the AR camera.

5. **Build**:
   - In **Build Settings**, click **Build** to generate the `.apk` file, or click **Build And Run** with your Android phone connected via USB debugging.

---

## 4. Mobile Testing & Backend Connection

### Localhost Limitation & Pinggy/ngrok Tunneling
An Android phone running the app cannot access the backend using `localhost` or `127.0.0.1`. You must host the backend on a secure, public **HTTPS** URL.

1. **Start Backend**:
   Navigate to the `backend/` directory on your PC and run:
   ```bash
   npm run dev
   ```
   The server starts locally on `http://localhost:3001`.

2. **Establish Tunnel**:
   Expose your local port `3001` via an HTTPS tunnel using Pinggy or ngrok:
   - **Pinggy**:
     ```bash
     ssh -R 80:localhost:3001 free@connect.pinggy.link
     ```
   - **ngrok**:
     ```bash
     ngrok http 3001
     ```
   This will give you a public HTTPS URL (e.g. `https://your-tunnel.pinggy.link` or `https://xxxx.ngrok-free.app`).

3. **Configure URL inside App**:
   - Enter your public HTTPS tunnel URL inside the input field at the top of the Android application.
   - The URL is automatically saved to **PlayerPrefs**, so you do not need to re-type it upon launching the application again.
   - **Warning Validation**: On an actual Android build/device, if the entered URL contains `localhost` or `127.0.0.1` or uses plain `http://` instead of `https://`, the status text will display a red warning:
     `"Warning: Use a public HTTPS backend URL such as Pinggy/ngrok, not localhost."`
     *(Note: This warning validation runs in actual Android builds to assist on-device debugging, and is bypassed in Editor Play Mode which defaults to local testing).*
