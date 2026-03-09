using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Plugins.UI.Editor
{
	[InitializeOnLoad]
	public static class UiPrefabProjectPreviewDrawer
	{
		private static readonly Dictionary<string, Texture2D> PreviewCache = new();

		static UiPrefabProjectPreviewDrawer()
		{
			EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
			AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
			EditorApplication.quitting += Cleanup;
		}

		private static void Cleanup()
		{
			foreach (var pair in PreviewCache)
			{
				if (pair.Value)
				{
					Object.DestroyImmediate(pair.Value);
				}
			}

			PreviewCache.Clear();
		}

		private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
		{
			if (selectionRect.width <= 20f || selectionRect.height <= 20f)
				return;

			string assetPath = AssetDatabase.GUIDToAssetPath(guid);
			if (string.IsNullOrEmpty(assetPath))
				return;

			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
			if (!prefab)
				return;

			if (!prefab.TryGetComponent<RectTransform>(out _))
				return;

			if (!PrefabUtility.IsPartOfPrefabAsset(prefab))
				return;

			if (!PreviewCache.TryGetValue(guid, out var preview) || !preview)
			{
				preview = GeneratePreview(prefab, assetPath, 256, 256);
				if (!preview)
					return;

				PreviewCache[guid] = preview;
			}

			Rect drawRect = GetIconRect(selectionRect);
			if (drawRect.width <= 0f || drawRect.height <= 0f)
				return;

			GUI.DrawTexture(drawRect, preview, ScaleMode.ScaleToFit, true);
		}

		private static Rect GetIconRect(Rect selectionRect)
		{
			bool isGrid = selectionRect.height > 20f;

			if (isGrid)
			{
				return new Rect(
					selectionRect.x + 2f,
					selectionRect.y + 2f,
					selectionRect.width - 4f,
					selectionRect.height - 18f
				);
			}

			float size = selectionRect.height - 2f;
			return new Rect(selectionRect.x + 1f, selectionRect.y + 1f, size, size);
		}

		private static Texture2D GeneratePreview(GameObject prefabRoot, string assetPath, int width, int height)
		{
			var rectTransform = prefabRoot.GetComponent<RectTransform>();
			if (!rectTransform)
				return null;

			var previewRender = new PreviewRenderUtility();
			previewRender.camera.backgroundColor = Color.clear;
			previewRender.camera.clearFlags = CameraClearFlags.SolidColor;
			previewRender.camera.cameraType = CameraType.Game;
			previewRender.camera.nearClipPlane = 0.1f;
			previewRender.camera.farClipPlane = 1000f;
			previewRender.camera.orthographic = true;
			previewRender.camera.cullingMask = ~0;

			GameObject instance = null;
			LayoutElement tempLayoutElement = null;
			bool createdTempLayoutElement = false;

			try
			{
				previewRender.BeginStaticPreview(new Rect(0f, 0f, width, height));

				instance = previewRender.InstantiatePrefabInScene(prefabRoot);
				if (!instance)
					return null;

				var canvas = instance.GetComponentInChildren<Canvas>(true);
				var rootRect = instance.GetComponent<RectTransform>();

				if (!rootRect)
					return null;

				bool isStretchRoot =
					!Mathf.Approximately(rootRect.anchorMin.x, rootRect.anchorMax.x) ||
					!Mathf.Approximately(rootRect.anchorMin.y, rootRect.anchorMax.y);

				if (isStretchRoot)
				{
					Vector2 size2 = rootRect.rect.size;

					if (size2.x <= 0.01f) size2.x = 1920;
					if (size2.y <= 0.01f) size2.y = 1080;

					rootRect.anchorMin = new Vector2(0.5f, 0.5f);
					rootRect.anchorMax = new Vector2(0.5f, 0.5f);
					rootRect.pivot = new Vector2(0.5f, 0.5f);
					rootRect.anchoredPosition = Vector2.zero;
					rootRect.localPosition = Vector3.zero;
					rootRect.localRotation = Quaternion.identity;
					rootRect.localScale = Vector3.one;

					rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size2.x);
					rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size2.y);
				}

				if (!canvas)
					canvas = instance.AddComponent<Canvas>();

				canvas.renderMode = RenderMode.WorldSpace;
				canvas.worldCamera = previewRender.camera;
				canvas.planeDistance = 1f;
				canvas.referencePixelsPerUnit = 100f;

				var scaler = canvas.GetComponent<CanvasScaler>();
				if (scaler)
					scaler.enabled = false;

				var canvasRect = canvas.GetComponent<RectTransform>();
				canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
				canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
				canvasRect.pivot = new Vector2(0.5f, 0.5f);
				canvasRect.anchoredPosition = Vector2.zero;
				canvasRect.localPosition = Vector3.zero;
				canvasRect.localRotation = Quaternion.identity;
				canvasRect.localScale = Vector3.one;

				rootRect.anchorMin = new Vector2(0.5f, 0.5f);
				rootRect.anchorMax = new Vector2(0.5f, 0.5f);
				rootRect.pivot = new Vector2(0.5f, 0.5f);
				rootRect.anchoredPosition = Vector2.zero;
				rootRect.localPosition = Vector3.zero;
				rootRect.localRotation = Quaternion.identity;
				rootRect.localScale = Vector3.one;

				var layoutElement = rootRect.GetComponent<LayoutElement>();

				Vector2 size = rootRect.rect.size;
				if (size.x <= 0.01f) size.x = 100f;
				if (size.y <= 0.01f) size.y = 100f;

				bool hasPreferredSizeFromLayout =
					layoutElement &&
					layoutElement.preferredWidth >= 0f &&
					layoutElement.preferredHeight >= 0f;

				bool hasDynamicLayout =
					rootRect.GetComponent<ContentSizeFitter>() ||
					rootRect.GetComponent<LayoutGroup>() ||
					hasPreferredSizeFromLayout;

				float currentWidth;
				float currentHeight;

				if (hasDynamicLayout)
				{
					var existingLayoutElement = rootRect.GetComponent<LayoutElement>();

					currentWidth =
						existingLayoutElement && existingLayoutElement.preferredWidth >= 0f
							? existingLayoutElement.preferredWidth
							: (rootRect.rect.width > 0.01f ? rootRect.rect.width : 100f);

					currentHeight =
						existingLayoutElement && existingLayoutElement.preferredHeight >= 0f
							? existingLayoutElement.preferredHeight
							: (rootRect.rect.height > 0.01f ? rootRect.rect.height : 100f);

					for (int i = 0; i < 8; i++)
					{
						rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, currentWidth);
						rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, currentHeight);

						Canvas.ForceUpdateCanvases();
						LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
						Canvas.ForceUpdateCanvases();

						float nextWidth =
							existingLayoutElement && existingLayoutElement.preferredWidth >= 0f
								? existingLayoutElement.preferredWidth
								: LayoutUtility.GetPreferredWidth(rootRect);

						float nextHeight =
							existingLayoutElement && existingLayoutElement.preferredHeight >= 0f
								? existingLayoutElement.preferredHeight
								: LayoutUtility.GetPreferredHeight(rootRect);

						if (nextWidth <= 0.01f)
							nextWidth = currentWidth;

						if (nextHeight <= 0.01f)
							nextHeight = currentHeight;

						bool widthStable = Mathf.Abs(nextWidth - currentWidth) < 0.5f;
						bool heightStable = Mathf.Abs(nextHeight - currentHeight) < 0.5f;

						currentWidth = nextWidth;
						currentHeight = nextHeight;

						if (widthStable && heightStable)
							break;
					}
				}
				else
				{
					currentWidth = Mathf.Max(rootRect.rect.width, 1f);
					currentHeight = Mathf.Max(rootRect.rect.height, 1f);
				}

				tempLayoutElement = rootRect.GetComponent<LayoutElement>();
				if (!tempLayoutElement)
				{
					tempLayoutElement = rootRect.gameObject.AddComponent<LayoutElement>();
					createdTempLayoutElement = true;
				}

				tempLayoutElement.ignoreLayout = false;
				tempLayoutElement.flexibleWidth = 0f;
				tempLayoutElement.flexibleHeight = 0f;
				tempLayoutElement.preferredWidth = currentWidth;
				tempLayoutElement.preferredHeight = currentHeight;
				tempLayoutElement.minWidth = currentWidth;
				tempLayoutElement.minHeight = currentHeight;

				rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, currentWidth);
				rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, currentHeight);

				Canvas.ForceUpdateCanvases();
				LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
				Canvas.ForceUpdateCanvases();

				FitCameraToRect(previewRender.camera, rootRect, width, height);

				previewRender.Render(true);

				Texture2D result = previewRender.EndStaticPreview();
				if (!result)
					return null;

				result.name = $"UI Preview {assetPath}";
				return result;
			}
			finally
			{
				if (createdTempLayoutElement && tempLayoutElement)
					Object.DestroyImmediate(tempLayoutElement);

				if (instance)
					Object.DestroyImmediate(instance);

				previewRender.camera.targetTexture = null;
				previewRender.Cleanup();
			}
		}

		private static void FitCameraToRect(Camera camera, RectTransform rectTransform, int width, int height)
		{
			var corners = new Vector3[4];

			Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
			Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

			foreach (var rt in rectTransform.GetComponentsInChildren<RectTransform>(true))
			{
				rt.GetWorldCorners(corners);

				for (int i = 0; i < 4; i++)
				{
					min = Vector3.Min(min, corners[i]);
					max = Vector3.Max(max, corners[i]);
				}
			}

			Vector3 center = (min + max) * 0.5f;
			Vector3 size = max - min;

			float rectWidth = Mathf.Max(size.x, 1f);
			float rectHeight = Mathf.Max(size.y, 1f);
			float aspect = width / (float)height;

			float orthoSizeByHeight = rectHeight * 0.5f;
			float orthoSizeByWidth = rectWidth * 0.5f / aspect;
			float orthoSize = Mathf.Max(orthoSizeByHeight, orthoSizeByWidth) * 1.1f;

			camera.transform.position = new Vector3(center.x, center.y, -10f);
			camera.transform.rotation = Quaternion.identity;
			camera.orthographicSize = orthoSize;
		}

		[MenuItem("Tools/UI/Clear UI Prefab Preview Cache")]
		private static void ClearCache()
		{
			Cleanup();
			EditorApplication.RepaintProjectWindow();
		}

		public static void Invalidate(string assetPath)
		{
			string guid = AssetDatabase.AssetPathToGUID(assetPath);
			if (string.IsNullOrEmpty(guid))
				return;

			if (PreviewCache.TryGetValue(guid, out var texture))
			{
				if (texture)
				{
					Object.DestroyImmediate(texture);
				}

				PreviewCache.Remove(guid);
			}
		}
	}

	public sealed class UiPrefabPreviewPostprocessor : AssetPostprocessor
	{
		private static void OnPostprocessAllAssets(
			string[] importedAssets,
			string[] deletedAssets,
			string[] movedAssets,
			string[] movedFromAssetPaths)
		{
			bool repaint = false;

			foreach (string path in importedAssets)
			{
				if (path.EndsWith(".prefab"))
				{
					UiPrefabProjectPreviewDrawer.Invalidate(path);
					repaint = true;
					break;
				}
			}

			if (repaint)
			{
				EditorApplication.delayCall += EditorApplication.RepaintProjectWindow;
			}
		}
	}

	[InitializeOnLoad]
	public static class UiPrefabPreviewInvalidation
	{
		static UiPrefabPreviewInvalidation()
		{
			PrefabStage.prefabSaved += OnPrefabSaved;
			ObjectChangeEvents.changesPublished += OnChangesPublished;
		}

		private static void OnPrefabSaved(GameObject prefabRoot)
		{
			var stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage == null)
				return;

			string path = stage.assetPath;
			if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
				return;

			InvalidatePrefabPreview(path);
		}

		private static void OnChangesPublished(ref ObjectChangeEventStream stream)
		{
			for (int i = 0; i < stream.length; i++)
			{
				var kind = stream.GetEventType(i);

				switch (kind)
				{
					case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
					{
						stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var e);
						TryInvalidateFromInstanceId(e.instanceId);
						break;
					}

					case ObjectChangeKind.CreateGameObjectHierarchy:
					{
						stream.GetCreateGameObjectHierarchyEvent(i, out var e);
						TryInvalidateFromInstanceId(e.instanceId);
						break;
					}

					case ObjectChangeKind.DestroyGameObjectHierarchy:
					{
						stream.GetDestroyGameObjectHierarchyEvent(i, out var e);
						TryInvalidateFromInstanceId(e.instanceId);
						break;
					}

					case ObjectChangeKind.ChangeChildrenOrder:
					{
						stream.GetChangeChildrenOrderEvent(i, out var e);
						TryInvalidateFromInstanceId(e.instanceId);
						break;
					}
				}
			}
		}

		private static void TryInvalidateFromInstanceId(int instanceId)
		{
			Object obj = EditorUtility.InstanceIDToObject(instanceId);
			if (!obj)
				return;

			string path = AssetDatabase.GetAssetPath(obj);
			if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
				return;

			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (!prefab || !prefab.TryGetComponent<RectTransform>(out _))
				return;

			InvalidatePrefabPreview(path);
		}

		private static void InvalidatePrefabPreview(string path)
		{
			UiPrefabProjectPreviewDrawer.Invalidate(path);
			EditorApplication.delayCall += EditorApplication.RepaintProjectWindow;
		}
	}
}
