using Godot;
using System;

namespace AceManager.UI
{
    public partial class InfoPopup : Control
    {
        private Label _titleLabel;
        private RichTextLabel _contentLabel;
        private Button _closeButton;

        public override void _Ready()
        {
            _titleLabel = GetNode<Label>("%TitleLabel");
            _contentLabel = GetNode<RichTextLabel>("%ContentLabel");
            _closeButton = GetNode<Button>("%CloseButton");

            _closeButton.Pressed += QueueFree;
        }

        public void ShowInfo(string title, string content)
        {
            _titleLabel.Text = title;
            _contentLabel.Text = content;
        }
    }
}
