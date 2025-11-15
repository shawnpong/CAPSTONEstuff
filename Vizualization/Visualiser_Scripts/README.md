# Unity Dog App

This Unity project implements an **Augmented Reality (AR) virtual pet dog** that mirrors the movements and behaviours of a physical robot through gesture-based commands captured by a glove. The system overlays a 3D dog model onto a QR code using **Vuforia Image Target tracking**, and responds to user input via **AES-encrypted TCP commands** sent from a Python client.

---

## Key Features

### AR Virtual Dog
- Uses **Vuforia ImageTarget** to detect and track a printed QR code mounted on a robot.
- A fully animated corgi model is anchored onto the robot in AR space.

### Gesture-Driven Behaviour
- Commands from the wearable glove are processed and forwarded to Unity through a Python TCP client.
- Dog actions include: **run**, **turn**, **eat**, **pet**, **angry**, and more.

### Food Throwing System
- User can choose between **Apple (+5 hunger)**, **Ham (+10 hunger)**, or **Chicken (+20 hunger)** via a UI food menu.
- Selected food item is thrown using a smooth Bézier flight arc and triggers eating animations on impact.

### Ball Throwing System
- Ball is thrown from the bottom-left of the screen into world space.
- After landing, it remains fixed in AR space until collected by the dog.
- Displays “Ball Collected!” feedback when the corgi collides with the ball.

### Stats & Reactions
- Hunger and happiness decrease over time.
- Special milestones at **20 → 10 → 0** trigger an *angry animation* and bark sound.

### Encrypted TCP Communication
- AES-CBC encrypted 16-byte packets received from a Python client.
- Unity interprets integer commands and triggers the corresponding animations/actions.

---

## Commands

| Command | Behaviour |
|--------|-----------|
| `1` | Come Here |
| `2` | Go Away |
| `3` | Turn around |
| `4` | Pet |
| `5` | Feed |
| `6` | Throw ball |

---

## Features

- **Open Food Menu**: Tap the button at the top-right of the screen.
- **Select Food** → Apple / Ham / Chicken → Confirm.
- **Feed Command** triggers the selected food throw.
- **Throw Ball Command** triggers a ball throw into AR space.
- **Sliders** display changing hunger & happiness levels.

---
