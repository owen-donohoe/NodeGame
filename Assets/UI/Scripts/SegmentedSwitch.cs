using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>One sliding face for two or three options. Buttons retain their binding names.</summary>
    public sealed class SegmentedSwitch : VisualElement
    {
        private readonly VisualElement track = new VisualElement();
        private readonly VisualElement thumb = new VisualElement { pickingMode = PickingMode.Ignore };
        private readonly Button[] faces;
        private LobbyMotion.Spring x, width;
        private bool placed;
        private int selected;
        private double lastTime;
        private readonly IVisualElementScheduledItem ticker;
        public event Action<int> Changed;

        public SegmentedSwitch(params Button[] options)
        {
            if (options.Length < 2 || options.Length > 3) throw new ArgumentException("Expected two or three options.");
            faces = options;
            AddToClassList("lb-segmented"); track.AddToClassList("lb-segmented__track");
            thumb.AddToClassList("lb-segmented__thumb"); track.Add(thumb); Add(track);
            for (int i = 0; i < faces.Length; i++)
            {
                int index = i;
                faces[i].ClearClassList(); faces[i].AddToClassList("lb-segmented__face");
                faces[i].style.marginLeft = i == 0 ? 0 : 4;
                track.Add(faces[i]);
                faces[i].clicked += () => { Select(index); Changed?.Invoke(index); };
                faces[i].RegisterCallback<GeometryChangedEvent>(_ => Retarget());
            }
            ticker = schedule.Execute(Tick).Every(16); ticker.Pause();
            RegisterCallback<AttachToPanelEvent>(_ => { if (!Settled) Wake(); });
            Select(0);
        }

        public static SegmentedSwitch Replace(VisualElement host, params Button[] options)
        {
            var parent = host.parent; int index = parent.IndexOf(host);
            var control = new SegmentedSwitch(options) { name = host.name };
            host.RemoveFromHierarchy(); parent.Insert(index, control); return control;
        }

        public void Select(int index)
        {
            selected = Mathf.Clamp(index, 0, faces.Length - 1);
            for (int i = 0; i < faces.Length; i++)
                faces[i].EnableInClassList("lb-segmented__face--on", i == selected);
            Retarget();
        }

        private void Retarget()
        {
            var face = faces[selected].layout;
            if (float.IsNaN(face.width) || face.width <= 0 || float.IsNaN(track.layout.width)) return;
            var target = SegmentedSwitchMath.ThumbTarget(face.x, face.width, track.layout.width);
            x.Target = target.X; width.Target = target.Width;
            if (!placed)
            {
                x = new LobbyMotion.Spring(target.X); width = new LobbyMotion.Spring(target.Width);
                placed = true; Paint();
            }
            else if (!Settled && panel != null) Wake();
        }

        private void Tick()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float dt = (float)(now - lastTime); lastTime = now;
            if (!placed) { ticker.Pause(); return; }
            if (!Settled) { x.Step(dt, 1f); width.Step(dt, 1f); Paint(); }
            if (Settled) { x.Position = x.Target; width.Position = width.Target; x.Velocity = width.Velocity = 0f; Paint(); ticker.Pause(); }
        }

        private bool Settled => x.Settled && width.Settled;
        private void Wake() { lastTime = Time.realtimeSinceStartupAsDouble; ticker.Resume(); }

        private void Paint()
        {
            thumb.style.translate = new Translate(x.Position, 0);
            thumb.style.width = width.Position;
        }
    }
}
