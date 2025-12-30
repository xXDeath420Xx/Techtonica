# Techtonica Dedicated Server
# Ubuntu 22.04 + Wine + Xvfb + SteamCMD
FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive
ENV DISPLAY=:99
ENV WINEDEBUG=-all
ENV WINEPREFIX=/home/steam/.wine

# Add 32-bit architecture for Wine
RUN dpkg --add-architecture i386

# Install dependencies
RUN apt-get update && apt-get install -y \
    # Wine and dependencies
    wine64 \
    wine32 \
    winetricks \
    # Virtual display
    xvfb \
    x11vnc \
    # SteamCMD dependencies
    lib32gcc-s1 \
    lib32stdc++6 \
    libsdl2-2.0-0:i386 \
    # Utilities
    curl \
    wget \
    ca-certificates \
    software-properties-common \
    # Process management
    supervisor \
    # Network tools for debugging
    net-tools \
    procps \
    && rm -rf /var/lib/apt/lists/*

# Create steam user (SteamCMD doesn't like running as root)
RUN useradd -m -s /bin/bash steam && \
    mkdir -p /home/steam/Steam && \
    mkdir -p /home/steam/.wine && \
    chown -R steam:steam /home/steam

# Install SteamCMD
RUN mkdir -p /opt/steamcmd && \
    cd /opt/steamcmd && \
    curl -sqL "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz" | tar zxvf - && \
    chown -R steam:steam /opt/steamcmd

# Create directories for game data
RUN mkdir -p /opt/techtonica/{game,saves,mods,bepinex,logs} && \
    chown -R steam:steam /opt/techtonica

# Copy scripts
COPY --chown=steam:steam scripts/entrypoint.sh /opt/techtonica/entrypoint.sh
COPY --chown=steam:steam scripts/update-game.sh /opt/techtonica/update-game.sh
RUN chmod +x /opt/techtonica/*.sh

# Supervisor config for process management
COPY supervisord.conf /etc/supervisor/conf.d/supervisord.conf

# Ports
# 6968 - Game server (Steam P2P relay)
# 6969 - Management API (future)
EXPOSE 6968/udp 6968/tcp 6969/tcp

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=120s --retries=3 \
    CMD pgrep -f "Techtonica.exe" > /dev/null || exit 1

USER steam
WORKDIR /opt/techtonica

ENTRYPOINT ["/opt/techtonica/entrypoint.sh"]
