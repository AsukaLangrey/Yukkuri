shader_type spatial;
render_mode diffuse_burley;
varying vec3 world_position;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
    vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
float segment(vec2 p,vec2 a,vec2 b) {
    vec2 ab=b-a; return length(p-a-ab*clamp(dot(p-a,ab)/dot(ab,ab),0.0,1.0));
}
void vertex() { world_position=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
    vec2 p=world_position.xz;
    float n=noise(p*0.4)*0.6+noise(p*2.1)*0.25+noise(p*11.0)*0.15;
    vec3 grass=mix(vec3(.20,.265,.12),vec3(.35,.405,.205),n);
    float ring=abs(length((p-vec2(-.5,.2))/vec2(9.0,6.6))-1.0)*6.6;
    float road=min(ring,segment(p,vec2(-7,-3.2),vec2(-7,1.0)));
    road=min(road,segment(p,vec2(-8,3.9),vec2(-13,8.6)));
    road=min(road,segment(p,vec2(1,6.6),vec2(2,13)));
    float edge=noise(p*4.1)*.20+noise(p*1.3)*.16;
    float dirt=1.0-smoothstep(.53+edge,.85+edge,road);
    vec3 soil=mix(vec3(.42,.33,.21),vec3(.63,.53,.35),n);
    ALBEDO=mix(grass,soil,dirt);
    ROUGHNESS=.96;
}
