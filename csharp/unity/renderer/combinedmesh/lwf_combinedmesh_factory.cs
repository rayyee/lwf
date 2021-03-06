/*
 * Copyright (C) 2012 GREE, Inc.
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty.  In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would be
 *    appreciated but is not required.
 * 2. Altered source versions must be plainly marked as such, and must not be
 *    misrepresented as being the original software.
 * 3. This notice may not be removed or altered from any source distribution.
 */

using UnityEngine;
using System;
using System.Collections.Generic;

using ResourceCache = LWF.UnityRenderer.ResourceCache;
using TextureLoader = System.Func<string, UnityEngine.Texture2D>;
using TextureUnloader = System.Action<UnityEngine.Texture2D>;

namespace LWF {
namespace CombinedMeshRenderer {

public class CombinedMeshBuffer
{
	public Vector3[] vertices;
	public Vector2[] uv;
	public int[] triangles;
	public Color32[] colors32;
	public Vector3[] additionalColors;
	public int[] objects;
	public int index;
	public bool modified;
	public bool initialized;

	public void Alloc(int n)
	{
		vertices = new Vector3[n * 4];
		uv = new Vector2[n * 4];
		triangles = new int[n * 6];
		colors32 = new Color32[n * 4];
		additionalColors = new Vector3[n * 4];
		objects = new int[n];
		index = 0;
		modified = true;
		initialized = true;

		for (int i = 0, j = 0; i < triangles.Length; i += 6, j += 4) {
			triangles[i + 0] = j + 0;
			triangles[i + 1] = j + 1;
			triangles[i + 2] = j + 2;
			triangles[i + 3] = j + 2;
			triangles[i + 4] = j + 1;
			triangles[i + 5] = j + 3;
		}
	}
}

public interface IMeshRenderer
{
	void UpdateMesh(CombinedMeshBuffer buffer);
}

public class CombinedMeshComponent : MonoBehaviour
{
	public int updateCount;
	public UnityEngine.MeshRenderer meshRenderer;
	public MeshFilter meshFilter;
	public CombinedMeshBuffer buffer;
	public Mesh mesh;
	public List<IMeshRenderer> renderers;
	public int rendererCount;
	public int rectangleCount;

	public void Init(Factory factory)
	{
		renderers = new List<IMeshRenderer>();

		mesh = new Mesh();
		mesh.name = "LWF/" + factory.data.name;
		mesh.MarkDynamic();

		meshFilter = gameObject.AddComponent<MeshFilter>();
		meshFilter.sharedMesh = mesh;

		meshRenderer = gameObject.AddComponent<UnityEngine.MeshRenderer>();
		if (!string.IsNullOrEmpty(factory.sortingLayerName))
			meshRenderer.sortingLayerName = factory.sortingLayerName;
		meshRenderer.sortingOrder = factory.sortingOrder;
		meshRenderer.castShadows = false;
		meshRenderer.receiveShadows = false;

		buffer = new CombinedMeshBuffer();
	}

	public void AddRenderer(IMeshRenderer renderer, int rc, int uc)
	{
		if (updateCount != uc) {
			updateCount = uc;
			rendererCount = 0;
			rectangleCount = 0;
		}

		int i = rendererCount++;
		if (i < renderers.Count)
			renderers[i] = renderer;
		else
			renderers.Add(renderer);

		rectangleCount += rc;
	}

	public void SetMaterial(Material material)
	{
		if (meshRenderer.sharedMaterial != material) {
			meshRenderer.sharedMaterial = material;
			buffer.modified = true;
		}
	}

	public void Disable()
	{
		updateCount = 0;
		rendererCount = 0;
		rectangleCount = 0;
		meshRenderer.sharedMaterial = null;
		mesh.Clear(true);
		gameObject.SetActive(false);
	}

	public void UpdateMesh()
	{
		gameObject.SetActive(true);

		if (buffer.objects == null || buffer.objects.Length != rectangleCount) {
			buffer.Alloc(rectangleCount);
		} else {
			buffer.index = 0;
		}

		for (int i = 0; i < rendererCount; ++i)
			renderers[i].UpdateMesh(buffer);

		if (buffer.modified) {
			buffer.modified = false;
			mesh.Clear(true);
			mesh.vertices = buffer.vertices;
			mesh.uv = buffer.uv;
			mesh.triangles = buffer.triangles;
			mesh.colors32 = buffer.colors32;
			mesh.normals = buffer.additionalColors;
			mesh.RecalculateBounds();
		}
	}

	void OnDestroy()
	{
		meshRenderer.sharedMaterial = null;
		meshFilter.sharedMesh = null;
		UnityEngine.MeshRenderer.Destroy(meshRenderer);
		MeshFilter.Destroy(meshFilter);
		Mesh.Destroy(mesh);
	}
}

public partial class Factory : UnityRenderer.Factory
{
	public int updateCount;
	private int meshComponentNo;
	private int usedMeshComponentNo;
	private List<CombinedMeshComponent> meshComponents;
	private CombinedMeshComponent currentMeshComponent;
	private Factory parent;

	public Factory(Data d, GameObject gObj,
			float zOff = 0, float zR = 1, int rQOff = 0,
			string sLayerName = null, int sOrder = 0, bool uAC = false,
			Camera renderCam = null, Camera inputCam = null,
			string texturePrfx = "", string fontPrfx = "",
			TextureLoader textureLdr = null,
			TextureUnloader textureUnldr = null,
			bool attaching = false)
		: base(d, gObj, zOff, zR, rQOff, sLayerName, sOrder, uAC, renderCam,
			inputCam, texturePrfx, fontPrfx, textureLdr, textureUnldr)
	{
		CreateBitmapContexts();
		CreateTextContexts();

		meshComponents = new List<CombinedMeshComponent>();
		if (!attaching)
			AddMeshComponent();
		usedMeshComponentNo = -1;

		updateCount = -1;
	}

	public override void Destruct()
	{
		foreach (CombinedMeshComponent meshComponent in meshComponents)
			GameObject.Destroy(meshComponent.gameObject);

		DestructBitmapContexts();
		DestructTextContexts();

		base.Destruct();
	}

	private CombinedMeshComponent AddMeshComponent()
	{
		GameObject gobj = new GameObject(
			"LWF/" + data.name + "/Mesh/" + meshComponents.Count);
		gobj.SetActive(false);
		gobj.transform.parent = gameObject.transform;
		gobj.transform.position = gameObject.transform.position;
		CombinedMeshComponent meshComponent =
			gobj.AddComponent<CombinedMeshComponent>();
		meshComponent.Init(this);
		meshComponents.Add(meshComponent);
		return meshComponent;
	}

	public override void BeginRender(LWF lwf)
	{
		base.BeginRender(lwf);

		parent = null;
		var lwfParent = lwf.GetParent();
		if (lwfParent != null)
			parent = lwfParent.rendererFactory as Factory;
		if (parent != null)
			return;

		updateCount = lwf.updateCount;
		meshComponentNo = -1;
		currentMeshComponent = null;
	}

	public void Render(
		IMeshRenderer renderer, int rectangleCount, Material material)
	{
		if (parent != null) {
			parent.Render(renderer, rectangleCount, material);
			return;
		}

		if (currentMeshComponent == null) {
			meshComponentNo = 0;
			currentMeshComponent = meshComponents[meshComponentNo];
			currentMeshComponent.SetMaterial(material);
		} else {
			Material componentMaterial =
				currentMeshComponent.meshRenderer.sharedMaterial;
			if (componentMaterial != material) {
				int no = ++meshComponentNo;
				if (no >= meshComponents.Count)
					AddMeshComponent();
				currentMeshComponent = meshComponents[no];
				currentMeshComponent.SetMaterial(material);
			}
		}

		currentMeshComponent.AddRenderer(renderer, rectangleCount, updateCount);
	}

	public override void EndRender(LWF lwf)
	{
		base.EndRender(lwf);

		if (parent != null)
			return;

		if (currentMeshComponent == null) {
			for (int i = 0; i <= usedMeshComponentNo; ++i)
				meshComponents[i].Disable();
			usedMeshComponentNo = -1;
			return;
		}

		for (int i = 0; i <= meshComponentNo; ++i)
			meshComponents[i].UpdateMesh();

		for (int i = meshComponentNo + 1; i <= usedMeshComponentNo; ++i)
			meshComponents[i].Disable();
		usedMeshComponentNo = meshComponentNo;
	}

	public override Renderer ConstructBitmap(
		LWF lwf, int objectId, Bitmap bitmap)
	{
		return new BitmapRenderer(lwf, m_bitmapContexts[objectId]);
	}

	public override Renderer ConstructBitmapEx(
		LWF lwf, int objectId, BitmapEx bitmapEx)
	{
		return new BitmapRenderer(lwf, m_bitmapExContexts[objectId]);
	}

	public override TextRenderer ConstructText(LWF lwf, int objectId, Text text)
	{
		return new TextMeshRenderer(lwf, m_textContexts[objectId]);
	}
}

}	// namespace CombinedMeshRenderer
}	// namespace LWF
