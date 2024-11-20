using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Serialization;
using UnityEngine;

public class EntityInfoUIManager : Singleton<EntityInfoUIManager>
{
    [SerializeField] float StartFadeDistance = default;
    [SerializeField] float DisappearDistance = default;

    [DontSerialize, HideInInspector] public List<EntityInfoUI> UIs = new();

    void OnValidate()
    {
        DisappearDistance = math.max(StartFadeDistance, DisappearDistance);
    }

    void Update()
    {
        foreach (EntityInfoUI item in UIs)
        {
            Vector3 screenPoint = MainCamera.Camera.WorldToScreenPoint(item.WorldPosition);
            bool isVisible = screenPoint.z > 0f && screenPoint.z < DisappearDistance;
            if (isVisible != item.IsVisible)
            {
                item.IsVisible = isVisible;
                item.CanvasGroup.alpha = 0f;
                item.gameObject.SetActive(isVisible);
            }
            if (!isVisible) continue;

            item.Foreground.fillAmount = item.Percent;
            item.CanvasGroup.alpha = math.clamp(1f - ((screenPoint.z - StartFadeDistance) / (DisappearDistance - StartFadeDistance)), 0f, 1f);

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

            item.Label.text = item.Team.ToString();

            screenPoint.z = 0f;
            RectTransform transform = item.GetComponent<RectTransform>();
            transform.anchoredPosition = screenPoint;
        }
    }
}
