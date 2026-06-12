using UnityEngine;

public class UIShine : MonoBehaviour
{
    public Material mat;
    public float speed = 1f;

    void Update()
    {
        float pos = Mathf.PingPong(Time.time * speed, 1f);
        mat.SetFloat("_ShinePosition", pos);
    }
}
