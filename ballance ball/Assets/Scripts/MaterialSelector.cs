using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaterialSelector : MonoBehaviour
{
    public Material[] materials;
    public MeshRenderer renderer;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SelectMat(int index)
    {
        if (materials != null && renderer != null && index >= 0 && index < materials.Length)
        {
            renderer.material = materials[index];
        }
    }
}
