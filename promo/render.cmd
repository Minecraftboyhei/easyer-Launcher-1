@echo off
rem 合成宣传视频：8 个分镜 xfade 转场串联
set FF=
for /f "usebackq delims=" %%i in (`where ffmpeg 2^>nul`) do set FF=%%i
if "%FF%"=="" set FF=C:\Users\Administrator\AppData\Local\Microsoft\WinGet\Links\ffmpeg.exe
if not exist "%FF%" (
  echo ffmpeg not found
  exit /b 1
)
cd /d "F:\easyer Launcher 1\promo"

"%FF%" -y ^
 -loop 1 -t 4.2 -i frames\f1_title.png ^
 -loop 1 -t 4.5 -i frames\f2_home.png ^
 -loop 1 -t 4.5 -i frames\f3_download.png ^
 -loop 1 -t 4.5 -i frames\f4_loaders.png ^
 -loop 1 -t 4.5 -i frames\f5_packs.png ^
 -loop 1 -t 4.5 -i frames\f6_mods.png ^
 -loop 1 -t 4.5 -i frames\f7_account.png ^
 -loop 1 -t 5.2 -i frames\f8_end.png ^
 -filter_complex "[0:v]fps=30,format=yuv420p[v0];[1:v]fps=30,format=yuv420p[v1];[2:v]fps=30,format=yuv420p[v2];[3:v]fps=30,format=yuv420p[v3];[4:v]fps=30,format=yuv420p[v4];[5:v]fps=30,format=yuv420p[v5];[6:v]fps=30,format=yuv420p[v6];[7:v]fps=30,format=yuv420p,fade=t=out:st=3.9:d=1.3[v7];[v0][v1]xfade=transition=fade:duration=0.6:offset=3.6[x1];[x1][v2]xfade=transition=slideleft:duration=0.6:offset=8.1[x2];[x2][v3]xfade=transition=slideleft:duration=0.6:offset=12.6[x3];[x3][v4]xfade=transition=circleopen:duration=0.6:offset=17.1[x4];[x4][v5]xfade=transition=slideleft:duration=0.6:offset=21.6[x5];[x5][v6]xfade=transition=slideleft:duration=0.6:offset=26.1[x6];[x6][v7]xfade=transition=fade:duration=0.6:offset=30.6,format=yuv420p[out]" ^
 -map "[out]" -c:v libx264 -preset medium -crf 19 -r 30 -movflags +faststart "EasyerLauncher- Promo.mp4"
echo EXIT=%ERRORLEVEL%
