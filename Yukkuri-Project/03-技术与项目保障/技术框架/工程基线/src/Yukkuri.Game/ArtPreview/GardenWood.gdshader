shader_type spatial;
uniform vec4 tint : source_color = vec4(.5,.32,.16,1);
varying vec3 local_position;
void vertex(){local_position=VERTEX;}
void fragment(){
    float grain=sin(local_position.y*3.0+sin(local_position.x*27.0+local_position.z*29.0)*.7);
    float line=pow(abs(sin(local_position.x*46.0+local_position.z*47.0+sin(local_position.y*1.9))),16.0);
    ALBEDO=tint.rgb*(.96+.025*grain-.022*line);
    ROUGHNESS=.88;
}
