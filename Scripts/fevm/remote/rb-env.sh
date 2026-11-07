# ROOTBORN Unity verification environment on FEVM. Source it: . ~/farmer-unity-verify/tools/rb-env.sh
export RB_HOME=$HOME/farmer-unity-verify
export RB_TOOLS=$RB_HOME/tools
export RB_UNITY=$HOME/Unity/Hub/Editor/6000.3.13f1/Editor/Unity
RB_X=$RB_TOOLS/root/x
RB_VGL=$RB_TOOLS/root/vgl/usr/lib
export RB_XENV="LD_LIBRARY_PATH=$RB_X/usr/lib/x86_64-linux-gnu:$RB_X/lib/x86_64-linux-gnu XKB_BINDIR=$RB_X/usr/bin"
export RB_DISPLAY=${RB_DISPLAY:-:97}
export PATH=$RB_TOOLS:$PATH

# Start a dedicated Xvfb (1920x1080, -noreset) on $RB_DISPLAY if not running.
rb_xvfb() {
  if pgrep -f "Xvfb $RB_DISPLAY " >/dev/null; then return 0; fi
  env $RB_XENV nohup setsid "$RB_X/usr/bin/Xvfb" $RB_DISPLAY -screen 0 1920x1080x24 -nolisten tcp -noreset \
    -xkbdir "$RB_X/usr/share/X11/xkb" > "$RB_HOME/xvfb${RB_DISPLAY#:}.log" 2>&1 &
  sleep 3
  pgrep -f "Xvfb $RB_DISPLAY " >/dev/null || { echo "Xvfb failed; see $RB_HOME/xvfb${RB_DISPLAY#:}.log"; return 1; }
}

# Run Unity on the GPU through VirtualGL (egl0, OpenGL core). Args go to Unity.
rb_unity_gpu() {
  DISPLAY=$RB_DISPLAY VGL_DISPLAY=egl0 LD_LIBRARY_PATH=$RB_VGL \
    LD_PRELOAD="$RB_VGL/libdlfaker.so:$RB_VGL/libvglfaker.so" "$RB_UNITY" -force-glcore "$@"
}

# Refuse to start when another Unity already owns the same project.
rb_project_busy() { pgrep -af "[E]ditor/Unity.*-projectPath $1( |$)" >/dev/null; }
