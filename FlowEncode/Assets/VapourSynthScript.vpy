import vapoursynth as vs
core = vs.core

clip = core.std.BlankClip(format=vs.YUV420P8,width=1920,height=1080,length=240,fpsnum=24000,fpsden=1001)

clip.set_output()
