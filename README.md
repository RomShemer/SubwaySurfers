# Subway Surfers Clone for PC (Unity 3D)

<br/>

🧩 **Active branch:** `main`  
A 3D endless-runner game built in Unity, inspired by Subway Surfers.
The player runs through an infinite subway world, dodging trains, jumping over ramps, rolling under obstacles, and collecting coins — all while being chased by a guard and his dog.

<br/>

🎮 **Gameplay Features:**

- Smooth Player Controls – Continuous running, jumping, rolling, and lane swapping.
- Smart Spawning System – Dynamic obstacle & train generation with zone checks and collision validation.
- Animated Guard Chase – Guard and dog follow with synchronized animations and catch sequences.
- Curved World Shader – Realistic curvature effect for depth perception.
- Dynamic Audio – Footsteps, jumps, crashes, and music managed by a global mixer.
- Game UI – Coin counter, sound toggle, and pause menu.

<br/>

⌨️ **Controls:**

| Action | Key | Description |
|--------|-----|-------------|
|  Move Left | ← | Switch to the left lane |
|  Move Right | → | Switch to the right lane |
|  Jump | ↑ | Jump over obstacles or onto ramps |
|  Roll / Slide | ↓ | Roll under barriers (can also trigger right after a jump) |

  <br/>
  <br/>

🧱 **Technical Details:**

- Engine: Unity 6000.0.54f1  
- Language: C#  
- Main Scripts:  
  - PlayerController – Handles input, movement, jump, and roll mechanics.  
  - ObstacleAndTrainSpawner – Intelligent spawning system for obstacles and trains.  
  - FollowGuard – Manages guard and dog animation logic.  
  - GameManager – Handles state, collisions, and reset flow.  
  - RoadCoinSpawner – Spawns coins dynamically along lanes.  
- Shader: SubwaySurfersCurveWorld.shadergraph

<br/>

🚀 **How to Run:**
1. Open the project in Unity Hub.
2. Load the scene: SubwaySurfers.unity
3.	Press Play to start the game

<br/>


<div style="font-size: 32px; font-weight: 1000; display: flex; align-items: center;">
  <h1>
	  <img src="https://github.com/user-attachments/assets/e88ca0c3-80fd-4bb9-906d-baa9911ce938"
		  width="28.5"
		  style="margin-right: 10px;">
	  Game Demo
  </h1>
</div>


<table align="center" style="border:none>
  <tr style="background-color: transparent;">
    <td align="center" style="border:none; padding:10px; background-color: transparent;">
        <video src="https://github.com/user-attachments/assets/8c11de1a-544f-4d33-8f28-57e80c5636c5"
        controls 
        style="width:100%; max-width:450px; border-radius:10px;"></video>
      </div>
    </td>
    <td align="center" style="border:none; padding:10px; background-color: transparent;">
        <video src="https://github.com/user-attachments/assets/b0e76d98-df34-414f-a75d-e582038edb75"
        controls 
        style="width:100%; max-width:450px; border-radius:10px;"></video>
      </div>
    </td>
