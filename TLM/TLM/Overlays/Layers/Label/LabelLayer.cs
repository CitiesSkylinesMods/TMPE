namespace TrafficManager.Manager.Overlays.Layers {
    using TrafficManager.Util.Extensions;
    using TrafficManager.API.Attributes;
    using TrafficManager.API.Traffic.Enums;
    using UnityEngine;
    using TrafficManager.Manager.Impl.OverlayManagerData;
    using TrafficManager.Overlays.Layers;
    using System.Collections.Generic;

    /// <summary>
    /// Renders basic text labels on-screen.
    /// </summary>
    internal sealed class LabelLayer
        : ILayer
    {
        private const float MAX_CAMERA_DISTANCE = 300f;
        private const float MAX_MOUSE_DISTANCE = 150f;

        private const int FONT_STEP_SIZE = 3;
        private const int FONT_MIN_SIZE = 6;
        private const int FONT_MAX_SIZE = 24;

        // TODO: Find some way to load balance across layers
        private const int MAX_UPDATE = 50;

        private readonly FastList<Task> tasks;
        private readonly FastList<int> slots; // indexes of 'deleted' slots in tasks list

        /* Layer instance */

        internal static LabelLayer Instance { get; private set; }

        static LabelLayer() {
            Instance = new LabelLayer();
        }

        internal LabelLayer() {
            tasks = new();
        }

        /* Layer state */

        public LayerState LayerState { get; private set; }

        public bool HasTasks =>
            tasks.Count() - slots.Count() > 0;

        /* Add tasks */

        public void MakeRoomFor(int numItems) =>
            tasks.MakeRoomFor(numItems - slots.m_size); // -ve ignored

        public void Add(ILabel owner, InstanceID id) {
            if (slots.m_size > 0) {

                ref var task = ref tasks.m_buffer[slots.Pop()];
                Add(ref task, owner, id);

            } else {

                if (tasks.m_size == tasks.m_buffer.Length)
                    tasks.MakeRoomFor(32);

                ref var task = ref tasks.m_buffer[tasks.m_size++];
                Add(ref task, owner, id);

            }

            LayerState |= LayerState.NeedsUpdate;
        }

        public void Add(ILabel owner, FastList<InstanceID> labels) {
            MakeRoomFor(labels.m_size);

            for (int id = 0; id < labels.m_size; id++) {

                if (slots.m_size > 0) {

                    ref var task = ref tasks.m_buffer[slots.Pop()];
                    Add(ref task, owner, labels.m_buffer[id]);

                } else {

                    if (tasks.m_size == tasks.m_buffer.Length)
                        tasks.MakeRoomFor(32);

                    ref var task = ref tasks.m_buffer[tasks.m_size++];
                    Add(ref task, owner, labels.m_buffer[id]);

                }

            }

            LayerState |= LayerState.NeedsUpdate;
        }

        private void Add(ref Task task, ILabel owner, InstanceID id) {
            task.ID = id;
            task.Owner = owner;
            task.State = TaskState.NeedsUpdate | TaskState.Hidden;
        }

        /* Reposition tasks (eg. due to camera move) - task will be hidden until repositioned */

        public void QueueReposition(InstanceID id) {
            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.DeletedOrHidden && task.ID == id) {
                    task.State |= TaskState.NeedsReposition;
                    LayerState |= LayerState.NeedsReposition;
                }
            }
        }

        [Spike]
        public void QueueReposition(HashSet<InstanceID> hash, bool deleteOthers = false) {
            if (deleteOthers) {
                slots.EnsureCapacity(tasks.Count() - hash.Count);
            }

            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.Deleted) {
                    if (hash.Contains(task.ID)) {
                        task.State |= TaskState.NeedsReposition;
                        LayerState |= LayerState.NeedsReposition;
                    } else if (deleteOthers && !task.Deleted && !task.ID.IsEmpty) {
                        task.State = TaskState.Deleted;
                        slots.Add(i);
                        task.Owner.OnLabelDeleted(ref task.ID);
                    }
                }
            }

            if (!HasTasks) Clear();
        }

        public void QueueReposition(Overlays overlay) {
            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.DeletedOrHidden && (task.Owner.Overlay & overlay) != 0) {
                    task.State |= TaskState.NeedsReposition;
                    LayerState |= LayerState.NeedsReposition;
                }
            }
        }

        public void QueueReposition(ILabel owner) {
            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.DeletedOrHidden && task.Owner == owner) {
                    task.State |= TaskState.NeedsReposition;
                    LayerState |= LayerState.NeedsReposition;
                }
            }
        }

        [Spike][OnGUI]
        public void Reposition(ref OverlayState state) {
            LayerState &= ~LayerState.NeedsReposition;

            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];

                if (!task.NeedsReposition) continue;

                RefreshTask(ref task, ref state, fullUpdate: false);
            }
        }

        /* Update tasks - task will be visible while waiting for update */

        internal void QueueUpdate(InstanceID id) {
            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.DeletedOrHidden && task.ID == id) {
                    task.State |= TaskState.NeedsUpdate;
                    LayerState |= LayerState.NeedsUpdate;
                }
            }
        }

        internal void QueueUpdate(HashSet<InstanceID> hash) {
            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.DeletedOrHidden && hash.Contains(task.ID)) {
                    task.State |= TaskState.NeedsUpdate;
                    LayerState |= LayerState.NeedsUpdate;
                }
            }
        }

        internal void QueueUpdate(Overlays overlay) {
            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.DeletedOrHidden && (task.Owner.Overlay & overlay) != 0) {
                    task.State |= TaskState.NeedsUpdate;
                    LayerState |= LayerState.NeedsUpdate;
                }
            }
        }

        internal void QueueUpdate(ILabel owner) {
            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.DeletedOrHidden && task.Owner == owner) {
                    task.State |= TaskState.NeedsUpdate;
                    LayerState |= LayerState.NeedsUpdate;
                }
            }
        }

        [Spike]
        public void Update(ref OverlayState state) {
            LayerState &= ~LayerState.NeedsUpdate;

            int numUpdate = 0;

            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];

                if (!task.NeedsUpdate) continue;

                if (++numUpdate > MAX_UPDATE) {
                    LayerState |= LayerState.NeedsUpdate;
                    return;
                }

                RefreshTask(ref task, ref state, fullUpdate: true);
            }
        }

        private void RefreshTask(ref Task task, ref OverlayState state, bool fullUpdate) {
            if (task.Style == null) {
                InitialiseTask(ref task, ref state);
            } else if (fullUpdate) {
                UpdateTaskData(ref task, ref state);
            }

            task.ScreenPos = state.WorldToScreenPoint(task.WorldPos);

            if (task.ScreenPos.z < 0) { // not on screen
                // todo: if hovered/clicked, end that
                LayerState |= fullUpdate ? LayerState.NeedsUpdate : LayerState.NeedsReposition;
                return;
            }

            float zoom = 1.0f / task.ScreenPos.z * 150f;
            float fontSize = task.TextSize * zoom;
            fontSize = Mathf.Floor(fontSize / FONT_STEP_SIZE) * FONT_STEP_SIZE;
            //fontSize = Mathf.Clamp(fontSize, FONT_MIN_SIZE, FONT_MAX_SIZE);
            task.Style.fontSize = (int)fontSize;

            // TODO: find some way to cache CalcSize or Bounds for use with !fullUpdate
            // Note the zoomed font affects CalcSize and thus Bounds... :/
            Vector2 size = task.Style.CalcSize(new GUIContent(task.Text));
            task.Bounds.Set(
                x: task.ScreenPos.x - (size.x / 2f),
                y: task.ScreenPos.y,
                width: size.x,
                height: size.y);

            if (!task.EveryFrame)
                task.State &= ~(fullUpdate ? TaskState.NeedsUpdate : TaskState.NeedsReposition);

            LayerState |= LayerState.NeedsRender;
        }

        private void InitialiseTask(ref Task task, ref OverlayState state) {
            task.Style = new();
            task.Text = new();
            task.Text.tooltip = string.Empty;
            UpdateTaskData(ref task, ref state);
        }

        private void UpdateTaskData(ref Task task, ref OverlayState state) {
            task.Text.text = task.Owner.UpdateLabelStyle(ref task.ID, task.Hovered, ref state, task.Style);
            task.TextSize = (byte)task.Style.fontSize; // cast removes 3 bytes from struct
            task.WorldPos = task.Owner.UpdateLabelWorldPos(ref task.ID, ref state);
        }

        /* Render tasks */

        [Hot]
        public void Render(ref OverlayConfig config, ref OverlayState state) {
            LayerState &= ~LayerState.NeedsRender;

            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];

                if (!task.NeedsRender) continue;

                GUI.Label(task.Bounds, task.Text, task.Style);

                LayerState |= LayerState.NeedsRender;
            }
        }

        /* Hide tasks - task will remain hidden until told otherwise */

        internal void Hide(InstanceID id) {
            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.DeletedOrHidden && task.ID == id) {
                    task.State |= TaskState.Hidden;
                    task.Owner.OnLabelHidden(ref task.ID);
                }
            }
        }

        internal void Hide(HashSet<InstanceID> hash) {
            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.DeletedOrHidden && hash.Contains(task.ID)) {
                    task.State |= TaskState.Hidden;
                    task.Owner.OnLabelHidden(ref task.ID);
                }
            }
        }

        internal void Hide(Overlays overlay) {
            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.DeletedOrHidden && (task.Owner.Overlay & overlay) != 0) {
                    task.State |= TaskState.Hidden;
                    task.Owner.OnLabelHidden(ref task.ID);
                }
            }
        }

        internal void Hide(ILabel owner) {
            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.DeletedOrHidden && task.Owner == owner) {
                    task.State |= TaskState.Hidden;
                    task.Owner.OnLabelHidden(ref task.ID);
                }
            }
        }

        /* Delete tasks */

        internal void Delete(InstanceID id) {
            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.Deleted && task.ID == id)
                    Delete(ref task, i);
            }

            if (!HasTasks) Clear();
        }

        internal void Delete(HashSet<InstanceID> hash) {
            slots.MakeRoomFor(hash.Count);

            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.Deleted && hash.Contains(task.ID))
                    Delete(ref task, i);
            }

            if (!HasTasks) Clear();
        }

        internal void Delete(Overlays overlay) {
            slots.EnsureCapacity(tasks.Count() / 2);

            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.Deleted && (task.Owner.Overlay & overlay) != 0)
                    Delete(ref task, i);
            }

            if (!HasTasks) Clear();
        }

        internal void Delete(ILabel owner) {
            slots.EnsureCapacity(tasks.Count() / 2);

            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];
                if (!task.Deleted && task.Owner == owner)
                    Delete(ref task, i);
            }

            if (!HasTasks) Clear();
        }

        private void Delete(ref Task task, int i) {
            task.State = TaskState.Deleted;
            slots.Add(i);
            task.Owner.OnLabelDeleted(ref task.ID);
        }

        /* Remove all tasks */

        [Cold]
        public void Clear() {
            tasks.Clear();
            slots.Clear();

            LayerState &= ~LayerState.AllNeeds;
        }

        [Cold]
        public void Release() {
            tasks.Release();
            slots.Release();

            LayerState &= ~LayerState.AllNeeds;
        }

        /* Interact tasks */

        [Hot]
        internal void InteractTasks(ref OverlayConfig config, ref OverlayState state) {

            for (int i = 0; i < tasks.m_size; i++) {
                ref var task = ref tasks.m_buffer[i];

                if (!task.NeedsRender) continue;

                bool hovered = task.Bounds.Contains(state.MousePos);

                if (hovered) { // cold:

                    if (!task.Owner.IsLabelInteractive(ref task.ID)) continue;

                    bool save = false;

                    if (task.Hovered != hovered) {
                        save = true;
                        task.Hovered = hovered;
                        task.State = Parse(task.Owner.OnLabelHovered(ref task.ID, hovered, ref state), task.State);
                    }

                    if (task.Clicked != state.PrimaryOrSecondary || task.Clicked != hovered) {
                        save = true;
                        task.Clicked = hovered && state.PrimaryOrSecondary;
                        task.State = Parse(task.Owner.OnLabelClicked(ref task.ID, hovered, ref state), task.State);
                    }

                    if (save) tasks[i] = task;
                    if (task.State == TaskState.Deleted) {
                        slots.Add(i);
                        task.Owner.OnLabelDeleted(ref task.ID);
                    }

                } else if (task.Hovered) { // cold:

                    task.Hovered = false;
                    task.State = Parse(task.Owner.OnLabelHovered(ref task.ID, false, ref state), task.State);

                    if (task.Clicked) {
                        task.Clicked = false;
                        task.State = Parse(task.Owner.OnLabelHovered(ref task.ID, false, ref state), task.State);
                    }

                    tasks[i] = task; // save
                    if (task.State == TaskState.Deleted) {
                        slots.Add(i);
                        task.Owner.OnLabelDeleted(ref task.ID);
                    }

                }
            }
        }

        [Cold]
        private TaskState Parse(TaskState? response, TaskState current) {
            if (!response.HasValue)
                return current;

            if (response.Value == TaskState.Deleted)
                return TaskState.Deleted;

            if ((response.Value & TaskState.NeedsReposition) != 0)
                LayerState |= LayerState.NeedsReposition;

            if ((response.Value & TaskState.NeedsUpdate) != 0)
                LayerState |= LayerState.NeedsUpdate;

            return current | response.Value;
        }

        /* Render Task: */

        protected struct Task {
            internal TaskState State;

            internal InstanceID ID;
            internal ILabel Owner;

            // Required to render/interact
            internal Rect Bounds;
            internal GUIContent Text;
            internal GUIStyle Style;

            // Used for updating/positioning
            internal Vector3 WorldPos;
            internal Vector3 ScreenPos;
            internal byte TextSize;

            internal bool Deleted =>
                State == TaskState.Deleted;

            internal bool DeletedOrHidden =>
                State == TaskState.Deleted || (State & TaskState.Hidden) != 0;

            internal bool EveryFrame =>
                State != TaskState.Deleted &&
                (State & TaskState.EveryFrame) != 0;

            internal bool NeedsReposition =>
                State != TaskState.Deleted &&
                (State & TaskState.NeedsReposition | TaskState.Hidden) == TaskState.NeedsReposition;

            internal bool NeedsUpdate =>
                State != TaskState.Deleted &&
                (State & TaskState.NeedsUpdate | TaskState.NeedsReposition) == TaskState.NeedsUpdate;

            internal bool NeedsRender =>
                State != TaskState.Deleted &&
                (State & TaskState.Hidden | TaskState.NeedsReposition) == 0;

            internal bool Hovered {
                get => (State & TaskState.Hovered) != 0;
                set {
                    if (value) {
                        State |= TaskState.Hovered;
                    } else {
                        State &= ~TaskState.Hovered;
                    }
                }
            }

            internal bool Clicked {
                get => (State & TaskState.Clicked) != 0;
                set {
                    if (value) {
                        State |= TaskState.Clicked;
                    } else {
                        State &= ~TaskState.Clicked;
                    }
                }
            }
        }
    }
}
