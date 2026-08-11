Shader "Hidden/VRCFury/Tests/MaterialExtensions" {
    Properties {
        _Float("Float", Float) = 1.25
        _Range("Range", Range(0, 1)) = 0.5
        _Color("Color", Color) = (0.1, 0.2, 0.3, 0.4)
        _Vector("Vector", Vector) = (1, 2, 3, 4)
        _Tex("Texture", 2D) = "white" {}
    }
    SubShader {
        Pass {}
    }
}
