using CSUtil.Commons;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TrafficManager.U.Events {
    public class UEventInlet
        : MonoBehaviour,
          IPointerDownHandler,
          IPointerClickHandler,
          IPointerUpHandler,
          IPointerExitHandler,
          IPointerEnterHandler {
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