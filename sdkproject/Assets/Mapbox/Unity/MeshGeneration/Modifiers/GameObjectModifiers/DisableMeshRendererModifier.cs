namespace Mapbox.Unity.MeshGeneration.Modifiers
{
	using Mapbox.Unity.MeshGeneration.Data;
	using Mapbox.Unity.MeshGeneration.Modifiers;
	using UnityEngine;

	[CreateAssetMenu(menuName = "Mapbox/Modifiers/Disable Mesh Renderer Modifier")]
	[HelpURL("https://www.mapbox.com/mapbox-unity-sdk/api/unity/Mapbox.Unity.MeshGeneration.Modiiers.DisableMeshRendererModifier.html")]
	public class DisableMeshRendererModifier : GameObjectModifier
	{
		public override void Run(VectorEntity ve, UnityTile tile)
		{
			ve.MeshRenderer.enabled = false;
		}
	}
}