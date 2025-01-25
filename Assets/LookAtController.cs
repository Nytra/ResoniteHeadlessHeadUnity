using System;
using UnityEngine;
using UnityEngine.Animations;

public class LookAtController : MonoBehaviour
{
    public LookAtConstraint lookAt;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        var campos = Camera.main.gameObject.transform.position;
        var thispos = transform.position;
        var dist = Vector3.Distance(thispos, campos);
        if (dist < 5)
        {
            lookAt.enabled = true;
        }
        else
        {
            lookAt.enabled = false;
			transform.localRotation = Quaternion.identity;
		}
    }
}
