using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ProcessorGUIManager : MonoBehaviour
{
    [SerializeField, NotNull] Canvas? _canvas = default;
    [SerializeField, NotNull] GameObject? _labelPrefab = default;
    [SerializeField, NotNull] GameObject? _imagePrefab = default;

    [SerializeField, NonReorderable, ReadOnly] List<Text> _labels = new();
    [SerializeField, NonReorderable, ReadOnly] List<Image> _images = new();

    void OnGUI()
    {
        if (ConnectionManager.ClientWorld == null) return;

        NativeList<UserUIElement> uiElements = ProcessorSystemClient.GetInstance(ConnectionManager.ClientWorld.Unmanaged).uiElements;

        int labelIndex = 0;
        int imageIndex = 0;

        for (int i = 0; i < uiElements.Length; i++)
        {
            UserUIElement uiElement = uiElements[i];
            switch (uiElement.Type)
            {
                case UserUIElementType.Label:
                    if (labelIndex >= _labels.Count)
                    {
                        GameObject o = Instantiate(_labelPrefab, _canvas.transform);
                        _labels.Add(o.GetComponent<Text>());
                    }
                    _labels[labelIndex].rectTransform.anchoredPosition = new Vector2(uiElement.Position.x, uiElement.Position.y);
                    _labels[labelIndex].rectTransform.sizeDelta = new Vector2(uiElement.Size.x, uiElement.Size.y);
                    _labels[labelIndex].text = uiElement.Label.Text.AsString().ToString();
                    labelIndex++;
                    break;
                case UserUIElementType.Image:
                    if (imageIndex >= _images.Count)
                    {
                        GameObject o = Instantiate(_imagePrefab, _canvas.transform);
                        _images.Add(o.GetComponent<Image>());
                    }
                    _images[imageIndex].rectTransform.anchoredPosition = new Vector2(uiElement.Position.x, uiElement.Position.y);
                    _images[imageIndex].rectTransform.sizeDelta = new Vector2(uiElement.Size.x, uiElement.Size.y);

                    if (_images[imageIndex].sprite == null ||
                        _images[imageIndex].sprite.texture.width != uiElement.Image.Width ||
                        _images[imageIndex].sprite.texture.height != uiElement.Image.Height)
                    {
                        if (_images[imageIndex].sprite != null)
                        {
                            if (_images[imageIndex].sprite.texture != null)
                            {
                                Destroy(_images[imageIndex].sprite.texture);
                            }
                            Destroy(_images[imageIndex].sprite);
                        }
                        _images[imageIndex].sprite = Sprite.Create(
                            new Texture2D(uiElement.Image.Width, uiElement.Image.Height),
                            new Rect(0, 0, uiElement.Image.Width, uiElement.Image.Height),
                            new Vector2(0f, 0f)
                        );
                        _images[imageIndex].sprite.texture.filterMode = FilterMode.Point;
                        _images[imageIndex].sprite.texture.wrapMode = TextureWrapMode.Clamp;
                    }

                    for (int y = 0; y < uiElement.Image.Height; y++)
                    {
                        for (int x = 0; x < uiElement.Image.Width; x++)
                        {
                            unsafe
                            {
                                byte p = ((byte*)&uiElement.Image.Image)[x + y * uiElement.Image.Width];
                                _images[imageIndex].sprite.texture.SetPixel(x, y, new Color(
                                    (float)((p >> 5) & 0b111) / (float)0b111,
                                    (float)((p >> 2) & 0b111) / (float)0b111,
                                    (float)((p >> 0) & 0b011) / (float)0b011,
                                    1f
                                ));
                            }
                        }
                    }
                    _images[imageIndex].sprite.texture.Apply();
                    imageIndex++;
                    break;
                case UserUIElementType.MIN:
                case UserUIElementType.MAX:
                default: throw new UnreachableException();
            }
        }

        for (int i = labelIndex; i < _labels.Count; i++)
        {
            Destroy(_labels[labelIndex].gameObject);
            _labels.RemoveAt(labelIndex);
        }

        for (int i = imageIndex; i < _images.Count; i++)
        {
            Destroy(_images[imageIndex].sprite.texture);
            Destroy(_images[imageIndex].sprite);
            Destroy(_images[imageIndex].gameObject);
            _images.RemoveAt(imageIndex);
        }
    }
}
