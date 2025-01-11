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
                item.gameObject.SetActive(isVisible);
            }

            if (!isVisible)
            {
                item.CanvasGroup.alpha = 0f;
                continue;
            }

            static void HandleBar(EntityInfoUIBar bar, float percent)
            {
                bar.Foreground.fillAmount = percent;
                bool barVisible = percent != default;
                if (bar.IsVisible != barVisible)
                {
                    bar.IsVisible = barVisible;
                    bar.Object.SetActive(barVisible);
                }
            }

            HandleBar(item.HealthBar, item.HealthPercent);
            HandleBar(item.BuildingProgress, item.BuildingProgressPercent);
            HandleBar(item.TransporterLoad, item.TransporterLoadPercent);
            HandleBar(item.TransporterProgress, item.TransporterProgressPercent);
            HandleBar(item.ExtractorProgress, item.ExtractorProgressPercent);

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

            // item.Label.text = item.Team.ToString();

            screenPoint.z = 0f;
            RectTransform transform = item.GetComponent<RectTransform>();
            transform.anchoredPosition = screenPoint;
        }
    }
}
