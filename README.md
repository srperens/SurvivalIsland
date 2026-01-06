# Survival Island

A first-person survival game built with Godot 4 and C#. You've survived a plane crash on a remote island and must gather resources, craft tools, and survive against the elements and wildlife.

## Features

- **First-person exploration** - Navigate a procedurally generated forest
- **Day/night cycle** - Dynamic sun and moonlight with dawn/dusk colors
- **Survival mechanics** - Manage health, hunger, thirst, and warmth
- **Resource gathering** - Harvest trees, pick berries, collect stones
- **Crafting system** - Create tools and items from gathered resources
- **Wildlife** - Deer, rabbits, wolves, and elephants roam the island
- **Inventory system** - Hotbar and full inventory with item icons
- **Weather system** - Dynamic weather affecting gameplay

## Controls

| Key | Action |
|-----|--------|
| WASD | Move |
| Mouse | Look around |
| Space | Jump |
| Shift | Sprint |
| Ctrl | Crouch |
| E | Interact |
| Tab | Open inventory |
| C | Open crafting |
| 1-5 | Select hotbar slot |
| Left Click | Attack/Use item |
| Right Click | Secondary action |

## Setup Instructions

### Windows

#### 1. Install .NET SDK

1. Download .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
2. Run the installer and follow the prompts
3. Verify installation by opening PowerShell and running:
   ```powershell
   dotnet --version
   ```

#### 2. Install Godot 4 with .NET support

1. Go to https://godotengine.org/download/windows/
2. Download **Godot Engine - .NET** (the version with C# support)
3. Extract the ZIP file to a folder (e.g., `C:\Godot`)
4. Run `Godot_v4.x-stable_mono_win64.exe`

#### 3. Clone and run the project

```powershell
git clone https://github.com/srperens/SurvivalIsland.git
cd SurvivalIsland
```

Open Godot, click "Import", navigate to the project folder, and select `project.godot`.

### Linux (Ubuntu/Debian)

#### 1. Install .NET SDK

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET SDK
sudo apt update
sudo apt install -y dotnet-sdk-8.0

# Verify installation
dotnet --version
```

#### 2. Install Godot 4 with .NET support

**Option A: Download from website**
```bash
# Download Godot .NET version
wget https://github.com/godotengine/godot/releases/download/4.2-stable/Godot_v4.2-stable_mono_linux_x86_64.zip
unzip Godot_v4.2-stable_mono_linux_x86_64.zip
cd Godot_v4.2-stable_mono_linux_x86_64

# Run Godot
./Godot_v4.2-stable_mono_linux.x86_64
```

**Option B: Using Flatpak**
```bash
# Install Flatpak if not installed
sudo apt install flatpak

# Add Flathub repository
flatpak remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo

# Install Godot Mono version
flatpak install flathub org.godotengine.GodotSharp

# Run Godot
flatpak run org.godotengine.GodotSharp
```

#### 3. Clone and run the project

```bash
git clone https://github.com/srperens/SurvivalIsland.git
cd SurvivalIsland
```

Open Godot, click "Import", navigate to the project folder, and select `project.godot`.

### Linux (Fedora)

#### 1. Install .NET SDK

```bash
sudo dnf install dotnet-sdk-8.0

# Verify installation
dotnet --version
```

#### 2. Install Godot 4

Follow the same steps as Ubuntu (download from website or use Flatpak).

### Linux (Arch)

#### 1. Install .NET SDK

```bash
sudo pacman -S dotnet-sdk

# Verify installation
dotnet --version
```

#### 2. Install Godot 4 with .NET support

```bash
# From AUR (using yay)
yay -S godot-mono-bin

# Or download from website (see Ubuntu instructions)
```

## Building the Project

After opening the project in Godot:

1. Go to **Project > Project Settings > Dotnet > Project**
2. Ensure the project assembly name is `SurvivalIsland`
3. Build the C# solution: **MSBuild > Build Solution** or press `Alt+B`
4. Press `F5` to run the game

If you encounter build errors:
```bash
cd /path/to/SurvivalIsland
dotnet restore
dotnet build
```

## Troubleshooting

### Renderer fallback
The game automatically detects your graphics capabilities:
1. **Forward+** (Vulkan/D3D12) - Modern GPUs, soft shadows, best quality
2. **OpenGL 3.3** - Automatic fallback for older GPUs

No manual configuration needed - it just works!

### Shadows look sharp
Soft shadows require the Forward+ renderer (Vulkan/D3D12). With `gl_compatibility`, shadows will be sharp.

### Game runs slowly
Try reducing the shadow quality in `project.godot`:
```
lights_and_shadows/directional_shadow/size=1024
```

## Project Structure

```
SurvivalIsland/
├── scenes/
│   ├── main.tscn              # Main game scene
│   ├── player/                # Player scenes
│   ├── animals/               # Animal scenes
│   ├── world/                 # Environment scenes
│   └── resources/             # Interactable objects
├── scripts/
│   ├── player/                # Player scripts
│   ├── ai/                    # Animal AI
│   ├── systems/               # Game systems
│   ├── resources/             # Resource scripts
│   └── ui/                    # UI scripts
├── resources/
│   ├── items/                 # Item definitions (.tres)
│   └── icons/                 # Item icons
└── assets/
    └── shaders/               # Visual shaders
```

## License

MIT License

## Credits

Built with [Godot Engine](https://godotengine.org/) 4.x and C#
