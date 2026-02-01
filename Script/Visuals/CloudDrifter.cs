using Godot;
using System;
using System.Collections.Generic;

namespace AceManager.Visuals
{
    public partial class CloudDrifter : Control
    {
        [Export] public Godot.Collections.Array<Texture2D> CloudTextures;
        [Export] public float SpawnInterval = 2.0f;
        [Export] public float SpeedMin = 50.0f;
        [Export] public float SpeedMax = 150.0f;
        [Export] public float ScaleMin = 0.5f;
        [Export] public float ScaleMax = 1.5f;
        [Export] public float OpacityMin = 0.3f;
        [Export] public float OpacityMax = 0.8f;

        private float _timeUntilNextSpawn = 0f;
        private Random _random = new Random();

        public override void _Ready()
        {
            // Pre-warm: Spawn some clouds initially so screen isn't empty
            for (int i = 0; i < 5; i++)
            {
                SpawnCloud(true);
            }
        }

        public override void _Process(double delta)
        {
            // Move children
            foreach (var child in GetChildren())
            {
                if (child is TextureRect cloud)
                {
                    float speed = (float)cloud.GetMeta("speed", 100.0f);
                    cloud.Position -= new Vector2(speed * (float)delta, 0);

                    // Check boundary
                    if (cloud.Position.X + cloud.Size.X * cloud.Scale.X < -100)
                    {
                        cloud.QueueFree();
                    }
                }
            }

            // Spawn new
            _timeUntilNextSpawn -= (float)delta;
            if (_timeUntilNextSpawn <= 0)
            {
                // Spawn a random bunch to avoid "UFO fleet" look
                int count = _random.Next(1, 4);
                for (int i = 0; i < count; i++)
                {
                    SpawnCloud();
                }
                _timeUntilNextSpawn = SpawnInterval * (0.5f + (float)_random.NextDouble() * 1.0f);
            }
        }

        private void SpawnCloud(bool randomX = false)
        {
            if (CloudTextures == null || CloudTextures.Count == 0) return;

            var tex = CloudTextures[_random.Next(CloudTextures.Count)];
            var cloud = new TextureRect();
            cloud.Texture = tex;
            cloud.MouseFilter = MouseFilterEnum.Ignore; // Don't block clicks

            // Randomize properties
            float scale = ScaleMin + (float)_random.NextDouble() * (ScaleMax - ScaleMin);
            cloud.Scale = new Vector2(scale, scale);

            float opacity = OpacityMin + (float)_random.NextDouble() * (OpacityMax - OpacityMin);
            cloud.Modulate = new Color(1, 1, 1, opacity);

            float speed = SpeedMin + (float)_random.NextDouble() * (SpeedMax - SpeedMin);
            cloud.SetMeta("speed", speed);

            // Position
            float y = (float)_random.NextDouble() * (Size.Y - (tex.GetHeight() * scale));
            float x = randomX ? (float)_random.NextDouble() * Size.X : Size.X + 100;

            cloud.Position = new Vector2(x, y);

            AddChild(cloud);
            // Send to back so it doesn't cover UI if this is a background layer? 
            // Actually user wants it to "obscure" the menu, so maybe front?
            // We'll let scene hierarchy decide z-order.
        }
    }
}
