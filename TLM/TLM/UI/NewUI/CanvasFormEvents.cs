namespace TrafficManager.UI.NewUI {
    using CSUtil.Commons;
    using UnityEngine;
    using UnityEngine.EventSystems;

    public class CanvasFormEvents
        : MonoBehaviour,
          IPointerDownHandler,
          IPointerClickHandler,
          IPointerUpHandler,
          IPointerExitHandler,
          IPointerEnterHandler,
          IBeginDragHandler,
          IDragHandler,
          IEndDragHandler {
        public void OnBeginDrag(PointerEventData eventData) {
            Log.Info("Drag Begin");
        }

        public void OnDrag(PointerEventData eventData) {
            Log.Info("Dragging");
        }

        public void OnEndDrag(PointerEventData eventData) {
            Log.Info("Drag Ended");
        }

        public void OnPointerClick(PointerEventData eventData) {
            Log.Info("Clicked: " + eventData.pointerCurrentRaycast.gameObject.name);
        }

        public void OnPointerDown(PointerEventData eventData) {
            Log.Info("Mouse Down: " + eventData.pointerCurrentRaycast.gameObject.name);
        }

        public void OnPointerEnter(PointerEventData eventData) {
            Log.Info("Mouse Enter");
        }

        public void OnPointerExit(PointerEventData eventData) {
            Log.Info("Mouse Exit");
        }

        public void OnPointerUp(PointerEventData eventData) {
            Log.Info("Mouse Up");
        }
    }
}