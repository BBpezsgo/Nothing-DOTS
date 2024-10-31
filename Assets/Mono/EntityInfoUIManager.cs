using System;
using Unity.Mathematics;
using UnityEngine;

public class EntityInfoUIManager : MonoBehaviour
{
    [SerializeField] float StartFadeDistance = default;
    [SerializeField] float DisappearDistance = default;

    void OnValidate()
    {
        DisappearDistance = Math.Max(StartFadeDistance, DisappearDistance);
    }

    void Update()
    {
        foreach (EntityInfoUI item in FindObjectsByType<EntityInfoUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Vector3 screenPoint = Camera.main.WorldToScreenPoint(item.WorldPosition);
            item.gameObject.SetActive(screenPoint.z > 0f && screenPoint.z < DisappearDistance);
            if (!item.gameObject.activeSelf) continue;

            item.Foreground.fillAmount = item.Percent;
            item.CanvasGroup.alpha = Math.Clamp(1f - ((screenPoint.z - StartFadeDistance) / (DisappearDistance - StartFadeDistance)), 0f, 1f);

            switch (item.SelectionStatus)
            {
                case SelectionStatus.None:
                    item.SelectionStatusIndicator.enabled = false;
                    break;
                case SelectionStatus.Candidate:
                    item.SelectionStatusIndicator.enabled = false;
                    break;
                case SelectionStatus.Selected:
                    item.SelectionStatusIndicator.enabled = true;
                    break;
            }

            screenPoint.z = 0f;
            RectTransform transform = item.GetComponent<RectTransform>();
            transform.anchoredPosition = screenPoint;
        }
    }
}
