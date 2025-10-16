Subway Surfers Clone for PC (Unity 3D)

A 3D endless-runner game built in Unity, inspired by Subway Surfers.
The player runs through an infinite subway world, dodging trains, jumping over ramps, rolling under obstacles, and collecting coins — all while being chased by a guard and his dog.


🎮 Gameplay Features:
	•	Smooth Player Controls – Continuous running, jumping, rolling, and lane swapping.
	•	Smart Spawning System – Dynamic obstacle & train generation with zone checks and collision validation.
	•	Animated Guard Chase – Guard and dog follow with synchronized animations and catch sequences.
	•	Curved World Shader – Realistic curvature effect for depth perception.
	•	Dynamic Audio – Footsteps, jumps, crashes, and music managed by a global mixer.
	•	Game UI – Coin counter, sound toggle, and pause menu.


⌨️ Controls:

| Action | Key | Description |
|--------|-----|-------------|
|  Move Left | ← | Switch to the left lane |
|  Move Right | → | Switch to the right lane |
|  Jump | ↑ | Jump over obstacles or onto ramps |
|  Roll / Slide | ↓ | Roll under barriers (can also trigger right after a jump) |

  
🧱 Technical Details
	•	Engine: Unity 6000.0.54f1
	•	Language: C#
	•	Main Scripts:
	  •	PlayerController – Handles input, movement, jump, and roll mechanics.
    •	ObstacleAndTrainSpawner – Intelligent spawning system for obstacles and trains.
	  •	FollowGuard – Manages guard and dog animation logic.
	  •	GameManager – Handles state, collisions, and reset flow.
	  •	RoadCoinSpawner – Spawns coins dynamically along lanes.
	  •	Shader: SubwaySurfersCurveWorld.shadergraph


🚀 How to Run
	1.	Open the project in Unity Hub.
	2.	Load the scene: SubwaySurfers.unity
	3.	Press Play to start the game

