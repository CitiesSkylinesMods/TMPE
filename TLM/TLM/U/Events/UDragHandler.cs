namespace TrafficManager.U.Events {
    using CSUtil.Commons;
    using UnityEngine;
    using UnityEngine.EventSystems;

    public class UDragHandler
        : MonoBehaviour,
          IBeginDragHandler,
          IDragHandler,
          IEndDragHandler {
        public UWindow DragWindow { private get; set; }

        private Vector3 dragLastPosition_;

        public void OnBeginDrag(PointerEventData eventData) {
            dragLastPosition_ = Input.mousePosition;
            Log.Info("Drag Started");
        }

        public void OnDrag(PointerEventData eventData) {
            Vector2 diff = Input.mousePosition - this.dragLastPosition_;
            this.dragLastPosition_ = eventData.position;
            this.DragWindow.OnDrag(diff);
        }

        public void OnEndDrag(PointerEventData eventData) {
            Log.Info("Drag Ended");
        }
    }
}