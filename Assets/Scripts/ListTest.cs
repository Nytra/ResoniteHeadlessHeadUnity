using UnityEngine;
using System.Collections.Generic;

namespace Thundagun
{
	public class RendererList : MonoBehaviour
	{
		public List<MeshRenderer> TheList = new();
		public List<SkinnedMeshRenderer> TheSkinnedList = new();
	}
}