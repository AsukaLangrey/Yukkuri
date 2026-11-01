from pathlib import Path
root=Path(__file__).resolve().parent.parent
out=root/'src/Yukkuri.Game/Assets/Icons';out.mkdir(parents=True,exist_ok=True)
shapes={
'home':'<path d="M4 14L16 4l12 10M7 13v15h18V13M13 28V18h6v10"/>',
'observe':'<path d="M2 16s5-9 14-9 14 9 14 9-5 9-14 9S2 16 2 16Z"/><circle cx="16" cy="16" r="4"/>',
'hand':'<path d="M9 18V8a2 2 0 014 0v7-10a2 2 0 014 0v10-8a2 2 0 014 0v9-5a2 2 0 014 0v10c0 10-17 10-20 2l-2-5c-1-4 3-4 6 0Z"/>',
'food':'<path d="M3 16h26c-1 15-25 15-26 0ZM16 14V4M16 9c-8-1-8-6-8-6s8 0 8 6ZM17 11c8-1 8-6 8-6s-8 0-8 6Z"/>'}
for name,shape in shapes.items():
    (out/(name+'.svg')).write_text('<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 32 32"><g fill="none" stroke="#4c6d58" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">'+shape+'</g></svg>',encoding='utf-8')
print('Generated four project-native SVG icons.')
