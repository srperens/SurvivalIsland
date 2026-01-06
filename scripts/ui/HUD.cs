using Godot;

namespace SurvivalIsland;

public partial class HUD : CanvasLayer
{
    [Export] public ProgressBar? HealthBar { get; set; }
    [Export] public ProgressBar? HungerBar { get; set; }
    [Export] public ProgressBar? ThirstBar { get; set; }
    [Export] public ProgressBar? WarmthBar { get; set; }

    [Export] public Label? InteractionPrompt { get; set; }
    [Export] public ProgressBar? InteractionProgress { get; set; }

    [Export] public HBoxContainer? HotbarContainer { get; set; }
    [Export] public Label? TimeLabel { get; set; }
    [Export] public TextureRect? WeatherIcon { get; set; }

    [Export] public Control? CrosshairNormal { get; set; }
    [Export] public Control? CrosshairInteract { get; set; }

    private PlayerStats? _playerStats;
    private PlayerInteraction? _playerInteraction;
    private InventorySystem? _inventory;
    private DayNightCycle? _dayNight;
    private WeatherSystem? _weather;

    private TextureRect[] _hotbarSlots = new TextureRect[5];
    private Panel? _inventoryPanel;
    private bool _inventoryOpen;

    public override void _Ready()
    {
        // Get node references directly since NodePath exports don't work in C#
        HealthBar = GetNodeOrNull<ProgressBar>("MarginContainer/VBoxContainer/TopBar/StatsContainer/HealthBar");
        HungerBar = GetNodeOrNull<ProgressBar>("MarginContainer/VBoxContainer/TopBar/StatsContainer/HungerBar");
        ThirstBar = GetNodeOrNull<ProgressBar>("MarginContainer/VBoxContainer/TopBar/StatsContainer/ThirstBar");
        WarmthBar = GetNodeOrNull<ProgressBar>("MarginContainer/VBoxContainer/TopBar/StatsContainer/WarmthBar");
        TimeLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/TopBar/TimeLabel");
        InteractionPrompt = GetNodeOrNull<Label>("BottomContainer/InteractionPrompt");
        InteractionProgress = GetNodeOrNull<ProgressBar>("BottomContainer/InteractionProgress");
        HotbarContainer = GetNodeOrNull<HBoxContainer>("BottomContainer/HotbarContainer");
        CrosshairNormal = GetNodeOrNull<Control>("CenterContainer/Crosshair/CrosshairNormal");
        CrosshairInteract = GetNodeOrNull<Control>("CenterContainer/Crosshair/CrosshairInteract");

        GD.Print($"HUD._Ready: TimeLabel={TimeLabel != null}");

        // Setup will be called when player is ready
        SetInteractionPrompt("");
        CreateInventoryPanel();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("inventory"))
        {
            ToggleInventory();
        }
    }

    public void Initialize(PlayerController player, DayNightCycle? dayNight, WeatherSystem? weather)
    {
        GD.Print("HUD.Initialize called");
        _playerStats = player.GetNode<PlayerStats>("PlayerStats");
        _playerInteraction = player.GetNode<PlayerInteraction>("PlayerInteraction");
        _inventory = player.GetNode<InventorySystem>("InventorySystem");
        _dayNight = dayNight;
        _weather = weather;

        GD.Print($"HUD: Stats={_playerStats != null}, Interaction={_playerInteraction != null}, Inventory={_inventory != null}");

        // Connect signals
        if (_playerStats != null)
        {
            _playerStats.HealthChanged += OnHealthChanged;
            _playerStats.HungerChanged += OnHungerChanged;
            _playerStats.ThirstChanged += OnThirstChanged;
            _playerStats.WarmthChanged += OnWarmthChanged;
        }

        if (_playerInteraction != null)
        {
            _playerInteraction.InteractionPromptChanged += SetInteractionPrompt;
            _playerInteraction.InteractionProgressChanged += SetInteractionProgress;
        }

        if (_inventory != null)
        {
            _inventory.InventoryChanged += UpdateHotbar;
            _inventory.HotbarSelectionChanged += OnHotbarSelectionChanged;
        }

        // Create hotbar slots
        SetupHotbar();
    }

    public override void _Process(double delta)
    {
        // Update time display
        if (TimeLabel != null)
        {
            if (_dayNight != null)
            {
                TimeLabel.Text = _dayNight.GetTimeString();
            }
            else
            {
                // Try to find DayNightCycle if not set
                _dayNight = GetTree().Root.FindChild("DayNightCycle", true, false) as DayNightCycle;
            }
        }
    }

    private void SetupHotbar()
    {
        if (HotbarContainer == null) return;

        // Clear existing children (in case of reload)
        foreach (var child in HotbarContainer.GetChildren())
        {
            child.QueueFree();
        }

        for (int i = 0; i < 5; i++)
        {
            var slot = new Panel();
            slot.CustomMinimumSize = new Vector2(64, 64);
            slot.Name = $"HotbarSlot{i}";

            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            styleBox.BorderColor = new Color(0.5f, 0.5f, 0.5f);
            styleBox.SetBorderWidthAll(2);
            slot.AddThemeStyleboxOverride("panel", styleBox);

            var icon = new TextureRect();
            icon.Name = "Icon";
            icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            icon.OffsetLeft = 4;
            icon.OffsetTop = 4;
            icon.OffsetRight = -4;
            icon.OffsetBottom = -16;
            slot.AddChild(icon);

            var amountLabel = new Label();
            amountLabel.Name = "Amount";
            amountLabel.HorizontalAlignment = HorizontalAlignment.Right;
            amountLabel.VerticalAlignment = VerticalAlignment.Bottom;
            amountLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            amountLabel.OffsetRight = -4;
            amountLabel.OffsetBottom = -2;
            slot.AddChild(amountLabel);

            var nameLabel = new Label();
            nameLabel.Name = "ItemName";
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            nameLabel.VerticalAlignment = VerticalAlignment.Center;
            nameLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            nameLabel.AddThemeFontSizeOverride("font_size", 9);
            nameLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            nameLabel.Visible = false;
            slot.AddChild(nameLabel);

            var keyLabel = new Label();
            keyLabel.Name = "Key";
            keyLabel.Text = (i + 1).ToString();
            keyLabel.HorizontalAlignment = HorizontalAlignment.Left;
            keyLabel.VerticalAlignment = VerticalAlignment.Top;
            keyLabel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            keyLabel.OffsetLeft = 4;
            keyLabel.OffsetTop = 2;
            keyLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            slot.AddChild(keyLabel);

            HotbarContainer.AddChild(slot);
            _hotbarSlots[i] = icon;
        }

        UpdateHotbar();
    }

    private void UpdateHotbar()
    {
        if (_inventory == null || HotbarContainer == null) return;

        for (int i = 0; i < 5; i++)
        {
            var slot = _inventory.GetHotbarSlot(i);
            var slotPanel = HotbarContainer.GetNode<Panel>($"HotbarSlot{i}");
            var icon = slotPanel?.GetNode<TextureRect>("Icon");
            var amountLabel = slotPanel?.GetNode<Label>("Amount");
            var nameLabel = slotPanel?.GetNode<Label>("ItemName");

            if (icon != null)
            {
                icon.Texture = slot.Item?.Icon;
            }

            // Show item name if no icon
            if (nameLabel != null)
            {
                if (slot.Item != null && slot.Item.Icon == null)
                {
                    nameLabel.Text = slot.Item.DisplayName;
                    nameLabel.Visible = true;
                }
                else
                {
                    nameLabel.Visible = false;
                }
            }

            if (amountLabel != null)
            {
                amountLabel.Text = slot.Amount > 1 ? slot.Amount.ToString() : "";
            }
        }
    }

    private void OnHotbarSelectionChanged(int slot)
    {
        if (HotbarContainer == null) return;

        for (int i = 0; i < 5; i++)
        {
            var slotPanel = HotbarContainer.GetNode<Panel>($"HotbarSlot{i}");
            if (slotPanel == null) continue;

            var style = slotPanel.GetThemeStylebox("panel") as StyleBoxFlat;
            if (style != null)
            {
                style.BorderColor = i == slot ?
                    new Color(1, 0.8f, 0.2f) :
                    new Color(0.5f, 0.5f, 0.5f);
            }
        }
    }

    private void OnHealthChanged(float value, float max)
    {
        if (HealthBar != null)
        {
            HealthBar.MaxValue = max;
            HealthBar.Value = value;
        }
    }

    private void OnHungerChanged(float value, float max)
    {
        if (HungerBar != null)
        {
            HungerBar.MaxValue = max;
            HungerBar.Value = value;
        }
    }

    private void OnThirstChanged(float value, float max)
    {
        if (ThirstBar != null)
        {
            ThirstBar.MaxValue = max;
            ThirstBar.Value = value;
        }
    }

    private void OnWarmthChanged(float value, float max)
    {
        if (WarmthBar != null)
        {
            WarmthBar.MaxValue = max;
            WarmthBar.Value = value;
        }
    }

    private void SetInteractionPrompt(string prompt)
    {
        if (InteractionPrompt != null)
        {
            InteractionPrompt.Text = prompt;
            InteractionPrompt.Visible = !string.IsNullOrEmpty(prompt);
        }

        // Update crosshair
        bool canInteract = !string.IsNullOrEmpty(prompt);
        if (CrosshairNormal != null) CrosshairNormal.Visible = !canInteract;
        if (CrosshairInteract != null) CrosshairInteract.Visible = canInteract;
    }

    private void SetInteractionProgress(float progress)
    {
        if (InteractionProgress != null)
        {
            InteractionProgress.Value = progress * 100;
            InteractionProgress.Visible = progress > 0;
        }
    }

    private void CreateInventoryPanel()
    {
        _inventoryPanel = new Panel();
        _inventoryPanel.Name = "InventoryPanel";
        _inventoryPanel.Visible = false;
        _inventoryPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _inventoryPanel.Size = new Vector2(660, 340);
        _inventoryPanel.Position = new Vector2(-330, -170);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        style.BorderColor = new Color(0.4f, 0.4f, 0.4f);
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(8);
        _inventoryPanel.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 10);
        vbox.OffsetLeft = 15;
        vbox.OffsetTop = 15;
        vbox.OffsetRight = -15;
        vbox.OffsetBottom = -15;
        _inventoryPanel.AddChild(vbox);

        var title = new Label();
        title.Text = "INVENTORY (Tab to close)";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var grid = new GridContainer();
        grid.Name = "InventoryGrid";
        grid.Columns = 10;
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        vbox.AddChild(grid);

        // Create 40 slots (10x4)
        for (int i = 0; i < 40; i++)
        {
            var slot = CreateSlotPanel($"InvSlot{i}");
            grid.AddChild(slot);
        }

        AddChild(_inventoryPanel);
    }

    private Panel CreateSlotPanel(string name)
    {
        var slot = new Panel();
        slot.Name = name;
        slot.CustomMinimumSize = new Vector2(56, 56);

        var slotStyle = new StyleBoxFlat();
        slotStyle.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        slotStyle.BorderColor = new Color(0.4f, 0.4f, 0.4f);
        slotStyle.SetBorderWidthAll(1);
        slot.AddThemeStyleboxOverride("panel", slotStyle);

        var icon = new TextureRect();
        icon.Name = "Icon";
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        icon.OffsetLeft = 4;
        icon.OffsetTop = 4;
        icon.OffsetRight = -4;
        icon.OffsetBottom = -16;
        slot.AddChild(icon);

        var nameLabel = new Label();
        nameLabel.Name = "ItemName";
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        nameLabel.VerticalAlignment = VerticalAlignment.Center;
        nameLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        nameLabel.AddThemeFontSizeOverride("font_size", 8);
        nameLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        nameLabel.Visible = false;
        slot.AddChild(nameLabel);

        var amount = new Label();
        amount.Name = "Amount";
        amount.HorizontalAlignment = HorizontalAlignment.Right;
        amount.VerticalAlignment = VerticalAlignment.Bottom;
        amount.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        amount.OffsetRight = -4;
        amount.OffsetBottom = -2;
        slot.AddChild(amount);

        return slot;
    }

    private void ToggleInventory()
    {
        _inventoryOpen = !_inventoryOpen;

        if (_inventoryPanel != null)
        {
            _inventoryPanel.Visible = _inventoryOpen;
            if (_inventoryOpen)
            {
                Input.MouseMode = Input.MouseModeEnum.Visible;
                UpdateInventoryPanel();
            }
            else
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }
    }

    private void UpdateInventoryPanel()
    {
        if (_inventory == null || _inventoryPanel == null) return;

        var grid = _inventoryPanel.GetNode<GridContainer>("VBoxContainer/InventoryGrid");
        if (grid == null) return;

        for (int i = 0; i < 40; i++)
        {
            int x = i % 10;
            int y = i / 10;
            var slot = _inventory.GetInventorySlot(x, y);

            var slotPanel = grid.GetNode<Panel>($"InvSlot{i}");
            if (slotPanel == null) continue;

            var icon = slotPanel.GetNode<TextureRect>("Icon");
            var amount = slotPanel.GetNode<Label>("Amount");
            var nameLabel = slotPanel.GetNode<Label>("ItemName");

            if (icon != null)
                icon.Texture = slot.Item?.Icon;

            // Show item name if no icon
            if (nameLabel != null)
            {
                if (slot.Item != null && slot.Item.Icon == null)
                {
                    nameLabel.Text = slot.Item.DisplayName;
                    nameLabel.Visible = true;
                }
                else
                {
                    nameLabel.Visible = false;
                }
            }

            if (amount != null)
                amount.Text = slot.Amount > 1 ? slot.Amount.ToString() : "";
        }
    }
}
