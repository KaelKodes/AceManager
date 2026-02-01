using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using AceManager.Core;

namespace AceManager.UI
{
    public partial class TrainingPanel : Control
    {
        private TrainingSession _session = new();
        private List<TrainingLesson> _lessons = TrainingLesson.GetAll();
        private List<CrewData> _roster;

        // UI Components
        private VBoxContainer _lessonList;
        private VBoxContainer _pilotList;
        private VBoxContainer _benefitsList;
        private Button _conductButton;
        private Label _instructorLabel;

        [Signal] public delegate void PanelClosedEventHandler();

        public override void _Ready()
        {
            _roster = GameManager.Instance.Roster.Roster;
            BuildUI();
            SelectLesson(_lessons[0]);
        }

        private void BuildUI()
        {
            // Dimming Background (Covers entire StatusPanel)
            var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
            dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(dim);

            // Panel Wrapper (Centered at fixed size)
            var wrapper = new Control
            {
                CustomMinimumSize = new Vector2(1100, 780),
                Size = new Vector2(1100, 780)
            };
            wrapper.SetAnchorsPreset(LayoutPreset.Center);
            wrapper.SetOffsetsPreset(LayoutPreset.Center);
            wrapper.GrowHorizontal = GrowDirection.Both;
            wrapper.GrowVertical = GrowDirection.Both;
            AddChild(wrapper);

            // Root Background (Paper)
            var bg = new TextureRect
            {
                Texture = GD.Load<Texture2D>("res://Assets/UI/Training/paper.png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                SelfModulate = new Color(1.0f, 1.0f, 1.0f)
            };
            bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            wrapper.AddChild(bg);

            // Brass Frame Overlay
            var frame = new NinePatchRect
            {
                Texture = GD.Load<Texture2D>("res://Assets/UI/Training/Frame.jpg"),
                PatchMarginLeft = 140,
                PatchMarginTop = 140,
                PatchMarginRight = 140,
                PatchMarginBottom = 140,
                MouseFilter = MouseFilterEnum.Ignore
            };
            frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            wrapper.AddChild(frame);

            // Content Container
            var mainVBox = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(850, 550)
            };
            mainVBox.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            mainVBox.SetOffsetsPreset(LayoutPreset.Center);
            mainVBox.GrowHorizontal = GrowDirection.Both;
            mainVBox.GrowVertical = GrowDirection.Both;
            wrapper.AddChild(mainVBox);

            // Title
            var title = new Label
            {
                Text = "SQUADRON TRAINING COMMAND",
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = "HeaderLarge"
            };
            title.AddThemeColorOverride("font_color", new Color(0.15f, 0.12f, 0.08f));
            title.AddThemeFontSizeOverride("font_size", 36);
            mainVBox.AddChild(title);

            var contentHBox = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 450) };
            mainVBox.AddChild(contentHBox);

            // Left Section: Lessons (No Scroll)
            var leftPanel = CreateSection(contentHBox, "LESSON CHOICES", 0.35f, false);
            _lessonList = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            leftPanel.AddChild(_lessonList);
            PopulateLessons();

            // Middle Section: Pilots (Scroll allowed but hidden h-scroll)
            var midPanel = CreateSection(contentHBox, "SQUADRON ROSTER", 0.35f, true);
            _pilotList = new VBoxContainer();
            midPanel.AddChild(_pilotList);
            PopulatePilots();

            // Right Section: Benefits (No Scroll)
            var rightPanel = CreateSection(contentHBox, "EXPECTED BENEFITS", 0.3f, false);
            _benefitsList = new VBoxContainer();
            rightPanel.AddChild(_benefitsList);

            // Footer
            var footer = new HBoxContainer { Alignment = HBoxContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(0, 80) };
            mainVBox.AddChild(footer);

            _conductButton = new Button
            {
                Text = "CONDUCT TRAINING",
                CustomMinimumSize = new Vector2(300, 50),
                ThemeTypeVariation = "ButtonPrimary"
            };
            _conductButton.Pressed += OnConductPressed;
            footer.AddChild(_conductButton);

            var closeBtn = new Button { Text = "CANCEL", CustomMinimumSize = new Vector2(100, 50) };
            closeBtn.Pressed += () => { EmitSignal(SignalName.PanelClosed); Hide(); };
            footer.AddChild(closeBtn);
        }

        private Control CreateSection(HBoxContainer parent, string title, float ratio, bool useScroll)
        {
            var vBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = ratio };
            parent.AddChild(vBox);

            var lbl = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
            lbl.AddThemeColorOverride("font_color", new Color(0.15f, 0.12f, 0.08f));
            lbl.AddThemeFontSizeOverride("font_size", 20);
            vBox.AddChild(lbl);

            if (useScroll)
            {
                var scroll = new ScrollContainer
                {
                    SizeFlagsVertical = SizeFlags.ExpandFill,
                    HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
                };
                vBox.AddChild(scroll);
                var container = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                scroll.AddChild(container);
                return container;
            }
            else
            {
                var container = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
                vBox.AddChild(container);
                return container;
            }
        }

        private void PopulateLessons()
        {
            for (int i = 0; i < _lessons.Count; i++)
            {
                var lesson = _lessons[i];
                var btn = new Button
                {
                    Text = lesson.Name,
                    CustomMinimumSize = new Vector2(0, 75),
                    Alignment = HorizontalAlignment.Left,
                    SizeFlagsVertical = SizeFlags.ExpandFill
                };

                string iconPath = lesson.Type switch
                {
                    TrainingLessonType.DeflectionShooting => "GunSkill.png",
                    TrainingLessonType.AdvancedFlight => "PilotingSkill.png",
                    TrainingLessonType.TacticalBriefing => "TacticsSkill.png",
                    TrainingLessonType.SquadronDrill => "TeamworkSkill.png",
                    TrainingLessonType.CommandPost => "CommandSkill.png",
                    _ => "GunSkill.png"
                };

                btn.Icon = GD.Load<Texture2D>($"res://Assets/UI/Training/{iconPath}");
                btn.ExpandIcon = true;
                btn.Pressed += () => SelectLesson(lesson);
                _lessonList.AddChild(btn);
            }
        }

        private void SelectLesson(TrainingLesson lesson)
        {
            _session.LessonType = lesson.Type;
            PopulatePilots();
            UpdateBenefits();
        }

        private void PopulatePilots()
        {
            foreach (var child in _pilotList.GetChildren()) child.QueueFree();

            var vintageDark = new Color(0.15f, 0.12f, 0.08f);
            var lesson = _lessons.Find(l => l.Type == _session.LessonType);

            // Sort roster by proficiency in current lesson's stats
            var sortedRoster = _roster
                .Where(p => p.Status == PilotStatus.Active)
                .OrderByDescending(p =>
                {
                    float sum = 0;
                    foreach (var stat in lesson.PrimaryStats) sum += p.GetEffectiveStat(stat);
                    return sum;
                })
                .ToList();

            foreach (var pilot in sortedRoster)
            {
                var hBox = new HBoxContainer
                {
                    CustomMinimumSize = new Vector2(0, 45),
                    Alignment = HBoxContainer.AlignmentMode.Center
                };
                _pilotList.AddChild(hBox);

                bool isAttending = _session.AttendeePilotIds.Contains(pilot.Name) || _session.AttendeePilotIds.Count == 0;
                if (_session.AttendeePilotIds.Count == 0) _session.AttendeePilotIds.Add(pilot.Name);

                var check = new CheckBox
                {
                    Text = pilot.Name,
                    ButtonPressed = isAttending,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                check.AddThemeColorOverride("font_color", vintageDark);
                check.AddThemeColorOverride("font_pressed_color", vintageDark);
                check.AddThemeColorOverride("font_hover_color", new Color(0.3f, 0.25f, 0.2f));

                check.Toggled += (on) =>
                {
                    if (on) { if (!_session.AttendeePilotIds.Contains(pilot.Name)) _session.AttendeePilotIds.Add(pilot.Name); }
                    else _session.AttendeePilotIds.Remove(pilot.Name);
                    UpdateBenefits();
                };
                hBox.AddChild(check);

                var instructorBtn = new Button
                {
                    Text = "Set Co-Instructor",
                    SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
                    CustomMinimumSize = new Vector2(140, 0),
                    Disabled = pilot.Name == _session.CoInstructorPilotId
                };
                instructorBtn.Pressed += () =>
                {
                    _session.CoInstructorPilotId = pilot.Name;
                    PopulatePilots(); // Refresh to update button states/disabled
                    UpdateBenefits();
                };
                hBox.AddChild(instructorBtn);
            }
        }

        private void UpdateBenefits()
        {
            foreach (var child in _benefitsList.GetChildren()) child.QueueFree();

            var vintageDark = new Color(0.15f, 0.12f, 0.08f);
            var lesson = _lessons.Find(l => l.Type == _session.LessonType);
            var inst = _roster.FirstOrDefault(p => p.Name == _session.CoInstructorPilotId);

            float baseXP = _session.CalculateBaseXP(GameManager.Instance.CurrentBase.TrainingFacilitiesRating);
            float bonus = _session.GetInstructorBonus(inst);

            Action<string, bool> addLabel = (text, header) =>
            {
                var lbl = new Label
                {
                    Text = text,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                lbl.AddThemeColorOverride("font_color", vintageDark);
                if (header) lbl.AddThemeFontSizeOverride("font_size", 18);
                _benefitsList.AddChild(lbl);
            };

            addLabel($"Lesson: {lesson.Name}", true);
            addLabel($"Co-Instructor: {(inst?.Name ?? "None")}", false);
            addLabel($"Bonus: +{(bonus - 1.0f) * 100:F0}%", false);
            addLabel("", false);

            addLabel("Stat Gains:", true);
            foreach (var stat in lesson.PrimaryStats)
            {
                addLabel($"  • {stat}: +{baseXP * bonus:F1} XP", false);
            }

            addLabel("", false);
            addLabel("Fatigue Recovery:", true);
            addLabel($"  • Attendees: -5", false);
            addLabel($"  • Skippers: -15", false);
        }

        private void OnConductPressed()
        {
            GameManager.Instance.CompleteTraining(_session);
            EmitSignal(SignalName.PanelClosed);
            Hide();
        }
    }
}
