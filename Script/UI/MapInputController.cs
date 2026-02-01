using Godot;
using System;

namespace AceManager.UI
{
    /// <summary>
    /// Handles input events for the CommandMapPanel, including panning and zooming.
    /// </summary>
    public class MapInputController
    {
        private readonly Control _inputSurface;
        private readonly Action<Vector2> _onPan;
        private readonly Action<float> _onZoom;
        private readonly Action<Vector2> _onCursorMove;
        private readonly Action<bool> _onActiveStateChanged;

        private float _currentZoom = 8f;
        private bool _isDragging = false;
        private bool _isActive = false;

        public float MinZoom { get; set; } = 2f;
        public float MaxZoom { get; set; } = 30f;
        public float ZoomStep { get; set; } = 1.15f;

        public MapInputController(Control inputSurface, Action<Vector2> onPan, Action<float> onZoom, Action<Vector2> onCursorMove, Action<bool> onActiveStateChanged)
        {
            _inputSurface = inputSurface;
            _onPan = onPan;
            _onZoom = onZoom;
            _onCursorMove = onCursorMove;
            _onActiveStateChanged = onActiveStateChanged;

            _inputSurface.GuiInput += HandleInput;
            _inputSurface.FocusExited += () => SetActive(false);
            _inputSurface.MouseDefaultCursorShape = Control.CursorShape.Drag;
        }

        public void SetZoom(float zoom)
        {
            _currentZoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
        }

        public void SetActive(bool active)
        {
            if (_isActive == active) return;
            _isActive = active;
            _onActiveStateChanged?.Invoke(active);

            if (!active) _isDragging = false;
        }

        private void HandleInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb)
            {
                HandleMouseButton(mb);
            }
            else if (@event is InputEventMouseMotion mm)
            {
                HandleMouseMotion(mm);
            }
        }

        private void HandleMouseButton(InputEventMouseButton mb)
        {
            if (mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (!_isActive)
                    {
                        _inputSurface.GrabFocus();
                        SetActive(true);
                    }
                    _isDragging = true;
                }
                else if (mb.ButtonIndex == MouseButton.WheelUp)
                {
                    if (!_isActive) SetActive(true);
                    float newZoom = Math.Min(MaxZoom, _currentZoom * ZoomStep);
                    if (Math.Abs(newZoom - _currentZoom) > 0.01f)
                    {
                        _currentZoom = newZoom;
                        _onZoom?.Invoke(_currentZoom);
                    }
                    _inputSurface.AcceptEvent();
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown)
                {
                    if (!_isActive) SetActive(true);
                    float newZoom = Math.Max(MinZoom, _currentZoom / ZoomStep);
                    if (Math.Abs(newZoom - _currentZoom) > 0.01f)
                    {
                        _currentZoom = newZoom;
                        _onZoom?.Invoke(_currentZoom);
                    }
                    _inputSurface.AcceptEvent();
                }
            }
            else // Released
            {
                if (mb.ButtonIndex == MouseButton.Left)
                    _isDragging = false;
            }
        }

        private void HandleMouseMotion(InputEventMouseMotion mm)
        {
            if (_isDragging && _isActive)
            {
                // Pan map
                Vector2 delta = mm.Relative / _currentZoom;
                _onPan?.Invoke(delta);
                _inputSurface.AcceptEvent();
            }

            if (_isActive)
            {
                _onCursorMove?.Invoke(mm.Position);
            }
        }

        public void Cleanup()
        {
            _inputSurface.GuiInput -= HandleInput;
        }
    }
}
