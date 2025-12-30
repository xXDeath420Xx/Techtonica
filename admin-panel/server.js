/**
 * Techtonica Server Admin Panel
 * Comprehensive server management system matching CertiFried theme
 */

const express = require('express');
const session = require('express-session');
const bcrypt = require('bcryptjs');
const http = require('http');
const https = require('https');
const fs = require('fs');
const path = require('path');
const { exec, spawn } = require('child_process');
const Database = require('better-sqlite3');
const crypto = require('crypto');

// Configuration
const CONFIG = {
    gameDir: '/home/death/techtonica-server/game/Techtonica',
    winePrefix: '/home/death/techtonica-server/wine',
    bepinexLog: '/home/death/techtonica-server/game/Techtonica/BepInEx/LogOutput.log',
    debugLog: '/home/death/techtonica-server/debug.log',
    gameLog: '/home/death/techtonica-server/game/Techtonica/game.log',
    modConfig: '/home/death/techtonica-server/game/Techtonica/BepInEx/config/com.community.techtonicadedicatedserver.cfg',
    savesDir: '/home/death/techtonica-server/saves',
    backupsDir: '/home/death/techtonica-server/backups',
    dbFile: '/home/death/techtonica-server/admin-panel/data/admin.db',
    port: 6969,
    basePath: '/techtonica-admin',
    sslCert: '/etc/letsencrypt/live/certifriedmultitool.com/fullchain.pem',
    sslKey: '/etc/letsencrypt/live/certifriedmultitool.com/privkey.pem'
};

// Ensure directories exist
['data', 'public/css', 'public/js'].forEach(dir => {
    const fullPath = path.join(__dirname, dir);
    if (!fs.existsSync(fullPath)) {
        fs.mkdirSync(fullPath, { recursive: true });
    }
});

if (!fs.existsSync(CONFIG.backupsDir)) {
    fs.mkdirSync(CONFIG.backupsDir, { recursive: true });
}

// Initialize database
const db = new Database(CONFIG.dbFile);

// Create tables
db.exec(`
    CREATE TABLE IF NOT EXISTS users (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        username TEXT UNIQUE NOT NULL,
        password TEXT NOT NULL,
        email TEXT,
        role TEXT DEFAULT 'viewer',
        created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
        created_by INTEGER,
        last_login DATETIME,
        is_active INTEGER DEFAULT 1,
        avatar TEXT,
        discord_id TEXT,
        discord_username TEXT
    );

    CREATE TABLE IF NOT EXISTS sessions (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        user_id INTEGER NOT NULL,
        session_token TEXT NOT NULL,
        ip_address TEXT,
        user_agent TEXT,
        created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
        expires_at DATETIME,
        FOREIGN KEY (user_id) REFERENCES users(id)
    );

    CREATE TABLE IF NOT EXISTS audit_log (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        user_id INTEGER,
        action TEXT NOT NULL,
        details TEXT,
        ip_address TEXT,
        created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
        FOREIGN KEY (user_id) REFERENCES users(id)
    );

    CREATE TABLE IF NOT EXISTS invites (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        code TEXT UNIQUE NOT NULL,
        role TEXT DEFAULT 'viewer',
        created_by INTEGER NOT NULL,
        used_by INTEGER,
        created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
        expires_at DATETIME,
        max_uses INTEGER DEFAULT 1,
        uses INTEGER DEFAULT 0,
        FOREIGN KEY (created_by) REFERENCES users(id),
        FOREIGN KEY (used_by) REFERENCES users(id)
    );

    CREATE TABLE IF NOT EXISTS server_config (
        key TEXT PRIMARY KEY,
        value TEXT,
        updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
        updated_by INTEGER
    );

    CREATE TABLE IF NOT EXISTS backups (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        filename TEXT NOT NULL,
        size INTEGER,
        type TEXT DEFAULT 'manual',
        created_by INTEGER,
        created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
        notes TEXT,
        FOREIGN KEY (created_by) REFERENCES users(id)
    );

    CREATE TABLE IF NOT EXISTS scheduled_tasks (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        type TEXT NOT NULL,
        schedule TEXT NOT NULL,
        enabled INTEGER DEFAULT 1,
        last_run DATETIME,
        next_run DATETIME,
        created_by INTEGER,
        created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
        FOREIGN KEY (created_by) REFERENCES users(id)
    );

    CREATE TABLE IF NOT EXISTS webhooks (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        name TEXT NOT NULL,
        url TEXT NOT NULL,
        events TEXT NOT NULL,
        enabled INTEGER DEFAULT 1,
        created_by INTEGER,
        created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
        FOREIGN KEY (created_by) REFERENCES users(id)
    );
`);

// Migrate existing database - add discord columns if missing
try {
    db.exec(`ALTER TABLE users ADD COLUMN discord_id TEXT`);
} catch (e) { /* Column already exists */ }
try {
    db.exec(`ALTER TABLE users ADD COLUMN discord_username TEXT`);
} catch (e) { /* Column already exists */ }

// Role hierarchy
const ROLES = {
    owner: { level: 100, name: 'Owner', color: '#fbbf24', icon: 'fa-crown' },
    admin: { level: 80, name: 'Admin', color: '#a78bfa', icon: 'fa-user-shield' },
    moderator: { level: 50, name: 'Moderator', color: '#60a5fa', icon: 'fa-user-check' },
    viewer: { level: 10, name: 'Viewer', color: '#9ca3af', icon: 'fa-eye' }
};

// Permission definitions
const PERMISSIONS = {
    'server.start': ['owner', 'admin'],
    'server.stop': ['owner', 'admin'],
    'server.restart': ['owner', 'admin'],
    'server.console': ['owner', 'admin', 'moderator'],
    'server.config': ['owner', 'admin'],
    'players.view': ['owner', 'admin', 'moderator', 'viewer'],
    'players.kick': ['owner', 'admin', 'moderator'],
    'players.ban': ['owner', 'admin'],
    'backups.view': ['owner', 'admin', 'moderator'],
    'backups.create': ['owner', 'admin'],
    'backups.restore': ['owner', 'admin'],
    'backups.delete': ['owner'],
    'users.view': ['owner', 'admin'],
    'users.create': ['owner', 'admin'],
    'users.edit': ['owner', 'admin'],
    'users.delete': ['owner'],
    'webhooks.manage': ['owner', 'admin'],
    'audit.view': ['owner', 'admin'],
    'settings.view': ['owner', 'admin', 'moderator', 'viewer'],
    'settings.edit': ['owner']
};

// Check if user exists, create default owner if not
const userCount = db.prepare('SELECT COUNT(*) as count FROM users').get();
if (userCount.count === 0) {
    const hashedPassword = bcrypt.hashSync('TechtonicaAdmin2024!', 10);
    db.prepare('INSERT INTO users (username, password, role) VALUES (?, ?, ?)').run('admin', hashedPassword, 'owner');
    console.log('Created default owner account (username: admin, password: TechtonicaAdmin2024!)');
    db.prepare('INSERT INTO audit_log (action, details) VALUES (?, ?)').run('system', 'Default owner account created');
}

// Initialize Express
const app = express();

// Middleware
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

// Strip basePath prefix from incoming requests (for reverse proxy)
app.use((req, res, next) => {
    if (req.path.startsWith(CONFIG.basePath)) {
        req.url = req.url.replace(CONFIG.basePath, '') || '/';
    }
    next();
});

// Session configuration
app.use(session({
    secret: process.env.SESSION_SECRET || crypto.randomBytes(32).toString('hex'),
    resave: false,
    saveUninitialized: false,
    cookie: {
        secure: false,
        httpOnly: true,
        maxAge: 7 * 24 * 60 * 60 * 1000 // 7 days
    }
}));

// Static files
app.use('/css', express.static(path.join(__dirname, 'public/css')));
app.use('/js', express.static(path.join(__dirname, 'public/js')));
app.use('/images', express.static(path.join(__dirname, 'public/images')));

// Helper functions
function hasPermission(userRole, permission) {
    const allowedRoles = PERMISSIONS[permission] || [];
    return allowedRoles.includes(userRole);
}

function requireAuth(req, res, next) {
    if (!req.session.userId) {
        if (req.xhr || req.path.startsWith('/api/')) {
            return res.status(401).json({ error: 'Unauthorized' });
        }
        return res.redirect(CONFIG.basePath + '/login');
    }

    const user = db.prepare('SELECT * FROM users WHERE id = ? AND is_active = 1').get(req.session.userId);
    if (!user) {
        req.session.destroy();
        if (req.xhr || req.path.startsWith('/api/')) {
            return res.status(401).json({ error: 'Unauthorized' });
        }
        return res.redirect(CONFIG.basePath + '/login');
    }

    req.user = user;
    next();
}

function requirePermission(permission) {
    return (req, res, next) => {
        if (!req.user || !hasPermission(req.user.role, permission)) {
            return res.status(403).json({ error: 'Permission denied' });
        }
        next();
    };
}

function auditLog(userId, action, details, ipAddress) {
    db.prepare('INSERT INTO audit_log (user_id, action, details, ip_address) VALUES (?, ?, ?, ?)')
        .run(userId, action, details, ipAddress);
}

// Server status tracking
async function checkServerRunning() {
    return new Promise((resolve) => {
        exec('ps -eo pid,etimes,args | grep "Techtonica.exe.*-batchmode" | grep -v grep | head -1', (err, stdout) => {
            const line = stdout.trim();
            if (!line) {
                resolve({ running: false, pid: null, uptime: 0 });
                return;
            }
            const parts = line.trim().split(/\s+/);
            const pid = parseInt(parts[0]) || null;
            const uptime = parseInt(parts[1]) || 0;
            resolve({ running: true, pid, uptime });
        });
    });
}

async function getSystemStats() {
    return new Promise((resolve) => {
        exec('free -b | grep Mem && cat /proc/loadavg && df -B1 / | tail -1', (err, stdout) => {
            const lines = stdout.trim().split('\n');
            const memParts = lines[0]?.split(/\s+/) || [];
            const totalMem = parseInt(memParts[1]) || 0;
            const usedMem = parseInt(memParts[2]) || 0;
            const loadParts = lines[1]?.split(/\s+/) || [];
            const load1 = parseFloat(loadParts[0]) || 0;
            const load5 = parseFloat(loadParts[1]) || 0;
            const load15 = parseFloat(loadParts[2]) || 0;
            const diskParts = lines[2]?.split(/\s+/) || [];
            const totalDisk = parseInt(diskParts[1]) || 0;
            const usedDisk = parseInt(diskParts[2]) || 0;

            resolve({
                memory: { total: totalMem, used: usedMem, percent: totalMem ? (usedMem / totalMem * 100).toFixed(1) : 0 },
                cpu: { load1, load5, load15 },
                disk: { total: totalDisk, used: usedDisk, percent: totalDisk ? (usedDisk / totalDisk * 100).toFixed(1) : 0 }
            });
        });
    });
}

function formatBytes(bytes) {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

function formatUptime(seconds) {
    const days = Math.floor(seconds / 86400);
    const hours = Math.floor((seconds % 86400) / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;
    const parts = [];
    if (days > 0) parts.push(`${days}d`);
    if (hours > 0) parts.push(`${hours}h`);
    if (minutes > 0) parts.push(`${minutes}m`);
    if (secs > 0 || parts.length === 0) parts.push(`${secs}s`);
    return parts.join(' ');
}

function parseConfig() {
    try {
        if (!fs.existsSync(CONFIG.modConfig)) return {};
        const content = fs.readFileSync(CONFIG.modConfig, 'utf-8');
        const config = {};
        let currentSection = '';
        content.split('\n').forEach(line => {
            line = line.trim();
            if (line.startsWith('[') && line.endsWith(']')) {
                currentSection = line.slice(1, -1);
                config[currentSection] = {};
            } else if (line && !line.startsWith('#') && !line.startsWith(';') && line.includes('=')) {
                const [key, ...valueParts] = line.split('=');
                const value = valueParts.join('=').trim();
                if (currentSection) {
                    config[currentSection][key.trim()] = value;
                }
            }
        });
        return config;
    } catch (err) {
        return {};
    }
}

function saveModConfig(config) {
    try {
        let content = `## Settings file was created by plugin Techtonica Dedicated Server\n\n`;
        for (const section in config) {
            content += `[${section}]\n\n`;
            for (const key in config[section]) {
                content += `${key} = ${config[section][key]}\n`;
            }
            content += '\n';
        }
        fs.writeFileSync(CONFIG.modConfig, content);
        return true;
    } catch (err) {
        return false;
    }
}

// Routes

// Login page
app.get('/login', (req, res) => {
    if (req.session.userId) {
        return res.redirect(CONFIG.basePath + '/');
    }
    res.sendFile(path.join(__dirname, 'public/login.html'));
});

// Register page (invite only)
app.get('/register', (req, res) => {
    res.sendFile(path.join(__dirname, 'public/register.html'));
});

// Main dashboard - serves the SPA
app.get('/', requireAuth, (req, res) => {
    res.sendFile(path.join(__dirname, 'public/dashboard.html'));
});

// API: Login
app.post('/api/auth/login', async (req, res) => {
    const { username, password } = req.body;

    if (!username || !password) {
        return res.status(400).json({ error: 'Username and password required' });
    }

    const user = db.prepare('SELECT * FROM users WHERE username = ? AND is_active = 1').get(username);

    if (!user || !bcrypt.compareSync(password, user.password)) {
        auditLog(null, 'login_failed', `Failed login attempt for: ${username}`, req.ip);
        return res.status(401).json({ error: 'Invalid credentials' });
    }

    db.prepare('UPDATE users SET last_login = CURRENT_TIMESTAMP WHERE id = ?').run(user.id);
    req.session.userId = user.id;
    auditLog(user.id, 'login', 'User logged in', req.ip);

    res.json({
        success: true,
        user: {
            id: user.id,
            username: user.username,
            role: user.role,
            roleInfo: ROLES[user.role]
        }
    });
});

// API: Register with invite
app.post('/api/auth/register', async (req, res) => {
    const { username, password, email, inviteCode } = req.body;

    if (!username || !password || !inviteCode) {
        return res.status(400).json({ error: 'Username, password, and invite code required' });
    }

    const invite = db.prepare(`
        SELECT * FROM invites WHERE code = ?
        AND (expires_at IS NULL OR expires_at > CURRENT_TIMESTAMP)
        AND (max_uses = 0 OR uses < max_uses)
    `).get(inviteCode);

    if (!invite) {
        return res.status(400).json({ error: 'Invalid or expired invite code' });
    }

    const existing = db.prepare('SELECT id FROM users WHERE username = ?').get(username);
    if (existing) {
        return res.status(400).json({ error: 'Username already taken' });
    }

    const hashedPassword = bcrypt.hashSync(password, 10);
    const result = db.prepare('INSERT INTO users (username, password, email, role, created_by) VALUES (?, ?, ?, ?, ?)')
        .run(username, hashedPassword, email || null, invite.role, invite.created_by);

    db.prepare('UPDATE invites SET uses = uses + 1, used_by = ? WHERE id = ?').run(result.lastInsertRowid, invite.id);
    auditLog(result.lastInsertRowid, 'register', `User registered with invite ${inviteCode}`, req.ip);

    res.json({ success: true });
});

// API: Logout
app.post('/api/auth/logout', (req, res) => {
    if (req.session.userId) {
        auditLog(req.session.userId, 'logout', 'User logged out', req.ip);
    }
    req.session.destroy();
    res.json({ success: true });
});

// API: Get current user
app.get('/api/auth/me', requireAuth, (req, res) => {
    res.json({
        id: req.user.id,
        username: req.user.username,
        email: req.user.email,
        role: req.user.role,
        roleInfo: ROLES[req.user.role],
        permissions: Object.keys(PERMISSIONS).filter(p => hasPermission(req.user.role, p)),
        createdAt: req.user.created_at,
        lastLogin: req.user.last_login,
        discordId: req.user.discord_id,
        discordUsername: req.user.discord_username
    });
});

// API: Change own password
app.post('/api/auth/change-password', requireAuth, (req, res) => {
    const { currentPassword, newPassword } = req.body;

    if (!currentPassword || !newPassword) {
        return res.status(400).json({ error: 'Current and new password required' });
    }

    if (!bcrypt.compareSync(currentPassword, req.user.password)) {
        return res.status(401).json({ error: 'Current password is incorrect' });
    }

    const hashedPassword = bcrypt.hashSync(newPassword, 10);
    db.prepare('UPDATE users SET password = ? WHERE id = ?').run(hashedPassword, req.user.id);
    auditLog(req.user.id, 'password_change', 'User changed their password', req.ip);
    res.json({ success: true });
});

// API: Link Discord account
app.post('/api/auth/link-discord', requireAuth, async (req, res) => {
    const { discordId } = req.body;

    if (!discordId) {
        // Unlink Discord
        db.prepare('UPDATE users SET discord_id = NULL, discord_username = NULL WHERE id = ?').run(req.user.id);
        auditLog(req.user.id, 'discord_unlink', 'User unlinked Discord account', req.ip);
        return res.json({ success: true, message: 'Discord account unlinked' });
    }

    // Validate Discord ID format (should be a snowflake - 17-19 digit number)
    if (!/^\d{17,19}$/.test(discordId)) {
        return res.status(400).json({ error: 'Invalid Discord ID format. Should be a 17-19 digit number.' });
    }

    // Check if this Discord ID is already linked to another account
    const existing = db.prepare('SELECT id, username FROM users WHERE discord_id = ? AND id != ?').get(discordId, req.user.id);
    if (existing) {
        return res.status(400).json({ error: `This Discord ID is already linked to user: ${existing.username}` });
    }

    // Try to fetch Discord user info
    let discordUsername = null;
    try {
        const discordRes = await fetch(`https://discord.com/api/v10/users/${discordId}`, {
            headers: {
                'Authorization': `Bot ${process.env.DISCORD_BOT_TOKEN || ''}`
            }
        });
        if (discordRes.ok) {
            const discordUser = await discordRes.json();
            discordUsername = discordUser.global_name || discordUser.username;
        }
    } catch (e) {
        // Failed to fetch Discord info, continue without username
    }

    db.prepare('UPDATE users SET discord_id = ?, discord_username = ? WHERE id = ?')
        .run(discordId, discordUsername, req.user.id);

    auditLog(req.user.id, 'discord_link', `User linked Discord account: ${discordId}`, req.ip);
    res.json({
        success: true,
        discordId,
        discordUsername,
        message: discordUsername ? `Linked to Discord: ${discordUsername}` : 'Discord ID linked (could not fetch username)'
    });
});

// API: Server status
app.get('/api/server/status', requireAuth, async (req, res) => {
    const serverStatus = await checkServerRunning();
    const systemStats = await getSystemStats();
    const config = parseConfig();

    res.json({
        server: {
            running: serverStatus.running,
            pid: serverStatus.pid,
            uptime: serverStatus.uptime,
            uptimeFormatted: formatUptime(serverStatus.uptime),
            address: '51.81.155.59:6968'
        },
        system: {
            memory: { ...systemStats.memory, totalFormatted: formatBytes(systemStats.memory.total), usedFormatted: formatBytes(systemStats.memory.used) },
            cpu: systemStats.cpu,
            disk: { ...systemStats.disk, totalFormatted: formatBytes(systemStats.disk.total), usedFormatted: formatBytes(systemStats.disk.used) }
        },
        config
    });
});

// API: Start server
app.post('/api/server/start', requireAuth, requirePermission('server.start'), async (req, res) => {
    const serverStatus = await checkServerRunning();

    if (serverStatus.running) {
        return res.status(400).json({ error: 'Server is already running' });
    }

    const config = parseConfig();
    config['Server'] = config['Server'] || {};
    config['Server']['AutoStartServer'] = 'true';
    config['Server']['HeadlessMode'] = 'true';
    config['General'] = config['General'] || {};
    config['General']['EnableDirectConnect'] = 'true';
    saveModConfig(config);

    const startScript = `
        cd ${CONFIG.gameDir} &&
        WINEPREFIX=${CONFIG.winePrefix} \\
        WINEDLLOVERRIDES="winhttp=n,b" \\
        DISPLAY=:99 \\
        wine Techtonica.exe -batchmode -nographics > ${CONFIG.gameLog} 2>&1 &
    `;

    exec(startScript, (err) => {
        if (err) {
            auditLog(req.user.id, 'server_start_failed', err.message, req.ip);
            return res.status(500).json({ error: 'Failed to start server' });
        }
        auditLog(req.user.id, 'server_start', 'Server started', req.ip);
        triggerWebhook('server_start', { user: req.user.username });
        res.json({ success: true, message: 'Server starting...' });
    });
});

// API: Stop server
app.post('/api/server/stop', requireAuth, requirePermission('server.stop'), async (req, res) => {
    const serverStatus = await checkServerRunning();

    if (!serverStatus.running) {
        return res.status(400).json({ error: 'Server is not running' });
    }

    exec(`kill ${serverStatus.pid}`, (err) => {
        if (err) {
            auditLog(req.user.id, 'server_stop_failed', err.message, req.ip);
            return res.status(500).json({ error: 'Failed to stop server' });
        }
        auditLog(req.user.id, 'server_stop', 'Server stopped', req.ip);
        triggerWebhook('server_stop', { user: req.user.username });
        res.json({ success: true, message: 'Server stopped' });
    });
});

// API: Restart server
app.post('/api/server/restart', requireAuth, requirePermission('server.restart'), async (req, res) => {
    const serverStatus = await checkServerRunning();

    const startServer = () => {
        const config = parseConfig();
        config['Server'] = config['Server'] || {};
        config['Server']['AutoStartServer'] = 'true';
        config['Server']['HeadlessMode'] = 'true';
        saveModConfig(config);

        const startScript = `
            cd ${CONFIG.gameDir} &&
            WINEPREFIX=${CONFIG.winePrefix} \\
            WINEDLLOVERRIDES="winhttp=n,b" \\
            DISPLAY=:99 \\
            wine Techtonica.exe -batchmode -nographics > ${CONFIG.gameLog} 2>&1 &
        `;

        exec(startScript, (err) => {
            if (err) {
                return res.status(500).json({ error: 'Failed to start server' });
            }
            auditLog(req.user.id, 'server_restart', 'Server restarted', req.ip);
            triggerWebhook('server_restart', { user: req.user.username });
            res.json({ success: true, message: 'Server restarting...' });
        });
    };

    if (serverStatus.running) {
        exec(`kill ${serverStatus.pid}`, async (err) => {
            if (err) {
                return res.status(500).json({ error: 'Failed to stop server' });
            }
            await new Promise(resolve => setTimeout(resolve, 3000));
            startServer();
        });
    } else {
        startServer();
    }
});

// API: Get console logs
app.get('/api/server/logs', requireAuth, requirePermission('server.console'), (req, res) => {
    const logFile = req.query.type === 'bepinex' ? CONFIG.bepinexLog : CONFIG.gameLog;
    const lines = parseInt(req.query.lines) || 100;

    exec(`tail -n ${lines} "${logFile}" 2>/dev/null || echo "Log file not found"`, (err, stdout) => {
        res.json({ logs: stdout });
    });
});

// API: Get server config
app.get('/api/server/config', requireAuth, requirePermission('server.config'), (req, res) => {
    if (!fs.existsSync(CONFIG.modConfig)) {
        return res.json({ config: null });
    }
    const config = fs.readFileSync(CONFIG.modConfig, 'utf8');
    res.json({ config });
});

// API: Save server config
app.post('/api/server/config', requireAuth, requirePermission('server.config'), (req, res) => {
    const { config } = req.body;
    if (!config) {
        return res.status(400).json({ error: 'Config required' });
    }
    fs.writeFileSync(CONFIG.modConfig, config);
    auditLog(req.user.id, 'config_update', 'Server configuration updated', req.ip);
    res.json({ success: true });
});

// API: List backups
app.get('/api/backups', requireAuth, requirePermission('backups.view'), (req, res) => {
    const backups = db.prepare(`
        SELECT b.*, u.username as created_by_username
        FROM backups b LEFT JOIN users u ON b.created_by = u.id
        ORDER BY b.created_at DESC
    `).all();
    res.json({ backups });
});

// API: Create backup
app.post('/api/backups', requireAuth, requirePermission('backups.create'), async (req, res) => {
    const { notes } = req.body;
    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    const filename = `backup-${timestamp}.zip`;
    const filepath = path.join(CONFIG.backupsDir, filename);

    const wineSavesDir = path.join(CONFIG.winePrefix, 'drive_c/users/death/AppData/LocalLow/Fire Hose Games/Techtonica/saves');

    exec(`cd "${wineSavesDir}" && zip -r "${filepath}" . 2>/dev/null || echo "No saves found"`, (err, stdout) => {
        if (err || stdout.includes('No saves found')) {
            return res.status(500).json({ error: 'Failed to create backup or no saves found' });
        }

        const stats = fs.statSync(filepath);
        db.prepare('INSERT INTO backups (filename, size, type, created_by, notes) VALUES (?, ?, ?, ?, ?)')
            .run(filename, stats.size, 'manual', req.user.id, notes || null);

        auditLog(req.user.id, 'backup_create', `Created backup: ${filename}`, req.ip);
        triggerWebhook('backup_created', { user: req.user.username, filename });
        res.json({ success: true, filename });
    });
});

// API: Restore backup
app.post('/api/backups/:id/restore', requireAuth, requirePermission('backups.restore'), async (req, res) => {
    const backup = db.prepare('SELECT * FROM backups WHERE id = ?').get(req.params.id);

    if (!backup) {
        return res.status(404).json({ error: 'Backup not found' });
    }

    const filepath = path.join(CONFIG.backupsDir, backup.filename);
    if (!fs.existsSync(filepath)) {
        return res.status(404).json({ error: 'Backup file not found' });
    }

    const serverStatus = await checkServerRunning();
    if (serverStatus.running) {
        return res.status(400).json({ error: 'Please stop the server before restoring a backup' });
    }

    const wineSavesDir = path.join(CONFIG.winePrefix, 'drive_c/users/death/AppData/LocalLow/Fire Hose Games/Techtonica/saves');

    exec(`rm -rf "${wineSavesDir}"/* && unzip -o "${filepath}" -d "${wineSavesDir}"`, (err) => {
        if (err) {
            return res.status(500).json({ error: 'Failed to restore backup' });
        }
        auditLog(req.user.id, 'backup_restore', `Restored backup: ${backup.filename}`, req.ip);
        res.json({ success: true });
    });
});

// API: Delete backup
app.delete('/api/backups/:id', requireAuth, requirePermission('backups.delete'), (req, res) => {
    const backup = db.prepare('SELECT * FROM backups WHERE id = ?').get(req.params.id);

    if (!backup) {
        return res.status(404).json({ error: 'Backup not found' });
    }

    const filepath = path.join(CONFIG.backupsDir, backup.filename);
    if (fs.existsSync(filepath)) {
        fs.unlinkSync(filepath);
    }

    db.prepare('DELETE FROM backups WHERE id = ?').run(req.params.id);
    auditLog(req.user.id, 'backup_delete', `Deleted backup: ${backup.filename}`, req.ip);
    res.json({ success: true });
});

// API: List users
app.get('/api/users', requireAuth, requirePermission('users.view'), (req, res) => {
    const users = db.prepare(`
        SELECT id, username, email, role, created_at, last_login, is_active,
               (SELECT username FROM users u2 WHERE u2.id = users.created_by) as created_by_username
        FROM users ORDER BY created_at DESC
    `).all();
    res.json({ users, roles: ROLES });
});

// API: Create user
app.post('/api/users', requireAuth, requirePermission('users.create'), (req, res) => {
    const { username, password, email, role } = req.body;

    if (!username || !password) {
        return res.status(400).json({ error: 'Username and password required' });
    }

    if (ROLES[role]?.level >= ROLES[req.user.role]?.level) {
        return res.status(403).json({ error: 'Cannot create user with equal or higher role' });
    }

    const existing = db.prepare('SELECT id FROM users WHERE username = ?').get(username);
    if (existing) {
        return res.status(400).json({ error: 'Username already taken' });
    }

    const hashedPassword = bcrypt.hashSync(password, 10);
    db.prepare('INSERT INTO users (username, password, email, role, created_by) VALUES (?, ?, ?, ?, ?)')
        .run(username, hashedPassword, email || null, role || 'viewer', req.user.id);

    auditLog(req.user.id, 'user_create', `Created user: ${username} with role ${role}`, req.ip);
    res.json({ success: true });
});

// API: Update user
app.put('/api/users/:id', requireAuth, requirePermission('users.edit'), (req, res) => {
    const { email, role, is_active, password } = req.body;
    const targetUser = db.prepare('SELECT * FROM users WHERE id = ?').get(req.params.id);

    if (!targetUser) {
        return res.status(404).json({ error: 'User not found' });
    }

    if (targetUser.id !== req.user.id && ROLES[targetUser.role]?.level >= ROLES[req.user.role]?.level) {
        return res.status(403).json({ error: 'Cannot edit user with equal or higher role' });
    }

    if (role && ROLES[role]?.level >= ROLES[req.user.role]?.level) {
        return res.status(403).json({ error: 'Cannot promote user to equal or higher role' });
    }

    let query = 'UPDATE users SET ';
    const params = [];
    const updates = [];

    if (email !== undefined) { updates.push('email = ?'); params.push(email); }
    if (role !== undefined) { updates.push('role = ?'); params.push(role); }
    if (is_active !== undefined) { updates.push('is_active = ?'); params.push(is_active ? 1 : 0); }
    if (password) { updates.push('password = ?'); params.push(bcrypt.hashSync(password, 10)); }

    if (updates.length === 0) {
        return res.status(400).json({ error: 'No updates provided' });
    }

    query += updates.join(', ') + ' WHERE id = ?';
    params.push(req.params.id);

    db.prepare(query).run(...params);
    auditLog(req.user.id, 'user_update', `Updated user: ${targetUser.username}`, req.ip);
    res.json({ success: true });
});

// API: Delete user
app.delete('/api/users/:id', requireAuth, requirePermission('users.delete'), (req, res) => {
    const targetUser = db.prepare('SELECT * FROM users WHERE id = ?').get(req.params.id);

    if (!targetUser) {
        return res.status(404).json({ error: 'User not found' });
    }

    if (targetUser.id === req.user.id) {
        return res.status(400).json({ error: 'Cannot delete yourself' });
    }

    if (ROLES[targetUser.role]?.level >= ROLES[req.user.role]?.level) {
        return res.status(403).json({ error: 'Cannot delete user with equal or higher role' });
    }

    db.prepare('DELETE FROM users WHERE id = ?').run(req.params.id);
    auditLog(req.user.id, 'user_delete', `Deleted user: ${targetUser.username}`, req.ip);
    res.json({ success: true });
});

// API: Create invite
app.post('/api/invites', requireAuth, requirePermission('users.create'), (req, res) => {
    const { role, expiresIn, maxUses } = req.body;

    if (role && ROLES[role]?.level >= ROLES[req.user.role]?.level) {
        return res.status(403).json({ error: 'Cannot create invite for equal or higher role' });
    }

    const code = Array.from({ length: 8 }, () =>
        'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789'[Math.floor(Math.random() * 55)]
    ).join('');

    let expiresAt = null;
    if (expiresIn) {
        const date = new Date();
        date.setHours(date.getHours() + parseInt(expiresIn));
        expiresAt = date.toISOString();
    }

    db.prepare('INSERT INTO invites (code, role, created_by, expires_at, max_uses) VALUES (?, ?, ?, ?, ?)')
        .run(code, role || 'viewer', req.user.id, expiresAt, maxUses || 1);

    auditLog(req.user.id, 'invite_create', `Created invite: ${code} for role ${role}`, req.ip);
    res.json({ success: true, code });
});

// API: List invites
app.get('/api/invites', requireAuth, requirePermission('users.view'), (req, res) => {
    const invites = db.prepare(`
        SELECT i.*, u.username as created_by_username
        FROM invites i LEFT JOIN users u ON i.created_by = u.id
        ORDER BY i.created_at DESC
    `).all();
    res.json({ invites });
});

// API: Delete invite
app.delete('/api/invites/:id', requireAuth, requirePermission('users.create'), (req, res) => {
    db.prepare('DELETE FROM invites WHERE id = ?').run(req.params.id);
    res.json({ success: true });
});

// API: Get audit log
app.get('/api/audit', requireAuth, requirePermission('audit.view'), (req, res) => {
    const limit = parseInt(req.query.limit) || 100;
    const offset = parseInt(req.query.offset) || 0;

    const logs = db.prepare(`
        SELECT a.*, u.username
        FROM audit_log a LEFT JOIN users u ON a.user_id = u.id
        ORDER BY a.created_at DESC LIMIT ? OFFSET ?
    `).all(limit, offset);

    const total = db.prepare('SELECT COUNT(*) as count FROM audit_log').get();
    res.json({ logs, total: total.count });
});

// API: Webhooks
app.get('/api/webhooks', requireAuth, requirePermission('webhooks.manage'), (req, res) => {
    const webhooks = db.prepare('SELECT * FROM webhooks ORDER BY created_at DESC').all();
    res.json({ webhooks });
});

app.post('/api/webhooks', requireAuth, requirePermission('webhooks.manage'), (req, res) => {
    const { name, url, events } = req.body;

    if (!name || !url || !events) {
        return res.status(400).json({ error: 'Name, URL, and events required' });
    }

    db.prepare('INSERT INTO webhooks (name, url, events, created_by) VALUES (?, ?, ?, ?)')
        .run(name, url, JSON.stringify(events), req.user.id);

    auditLog(req.user.id, 'webhook_create', `Created webhook: ${name}`, req.ip);
    res.json({ success: true });
});

app.delete('/api/webhooks/:id', requireAuth, requirePermission('webhooks.manage'), (req, res) => {
    db.prepare('DELETE FROM webhooks WHERE id = ?').run(req.params.id);
    res.json({ success: true });
});

// API: Get save files
app.get('/api/saves', requireAuth, (req, res) => {
    const wineSavesDir = path.join(CONFIG.winePrefix, 'drive_c/users/death/AppData/LocalLow/Fire Hose Games/Techtonica/saves');

    if (!fs.existsSync(wineSavesDir)) {
        return res.json({ saves: [] });
    }

    const saves = [];
    const walkSync = (dir, base = '') => {
        try {
            fs.readdirSync(dir).forEach(file => {
                const fullPath = path.join(dir, file);
                const relPath = base ? `${base}/${file}` : file;
                const stat = fs.statSync(fullPath);
                if (stat.isDirectory()) {
                    walkSync(fullPath, relPath);
                } else if (file.endsWith('.dat')) {
                    saves.push({
                        name: relPath,
                        size: stat.size,
                        sizeFormatted: formatBytes(stat.size),
                        modified: stat.mtime
                    });
                }
            });
        } catch (err) { }
    };

    walkSync(wineSavesDir);
    saves.sort((a, b) => new Date(b.modified) - new Date(a.modified));
    res.json({ saves });
});

// Webhook trigger function with rich Discord embeds
async function triggerWebhook(event, data) {
    const webhooks = db.prepare('SELECT * FROM webhooks WHERE enabled = 1').all();

    // Event configurations for rich formatting
    const eventConfig = {
        server_start: {
            title: '🟢 Server Started',
            color: 0x22c55e,  // Green
            description: 'The Techtonica dedicated server has been started.'
        },
        server_stop: {
            title: '🔴 Server Stopped',
            color: 0xef4444,  // Red
            description: 'The Techtonica dedicated server has been stopped.'
        },
        server_restart: {
            title: '🔄 Server Restarted',
            color: 0xf59e0b,  // Amber
            description: 'The Techtonica dedicated server has been restarted.'
        },
        backup_created: {
            title: '💾 Backup Created',
            color: 0x3b82f6,  // Blue
            description: 'A new server backup has been created.'
        },
        player_join: {
            title: '👋 Player Joined',
            color: 0x22c55e,  // Green
            description: 'A player has joined the server.'
        },
        player_leave: {
            title: '👋 Player Left',
            color: 0xf59e0b,  // Amber
            description: 'A player has left the server.'
        },
        default: {
            title: '📢 Server Event',
            color: 0xa78bfa,  // Purple (CertiFried theme)
            description: 'A server event has occurred.'
        }
    };

    for (const webhook of webhooks) {
        try {
            const events = JSON.parse(webhook.events || '[]');
            if (!events.includes(event) && !events.includes('all')) continue;

            const config = eventConfig[event] || eventConfig.default;

            // Build fields from data
            const fields = [];
            if (data.user) {
                // Check if user has linked Discord
                const userInfo = db.prepare('SELECT discord_id, discord_username FROM users WHERE username = ?').get(data.user);
                const userDisplay = userInfo?.discord_id
                    ? `<@${userInfo.discord_id}>`
                    : data.user;
                fields.push({ name: '👤 Triggered By', value: userDisplay, inline: true });
            }
            if (data.filename) {
                fields.push({ name: '📁 File', value: `\`${data.filename}\``, inline: true });
            }
            if (data.player) {
                fields.push({ name: '🎮 Player', value: data.player, inline: true });
            }
            if (data.reason) {
                fields.push({ name: '📝 Reason', value: data.reason, inline: false });
            }

            // Add any extra data as fields
            Object.keys(data).forEach(key => {
                if (!['user', 'filename', 'player', 'reason'].includes(key)) {
                    fields.push({
                        name: key.charAt(0).toUpperCase() + key.slice(1).replace(/_/g, ' '),
                        value: String(data[key]),
                        inline: true
                    });
                }
            });

            const payload = {
                embeds: [{
                    title: config.title,
                    description: config.description,
                    color: config.color,
                    fields: fields,
                    thumbnail: {
                        url: 'https://cdn.thunderstore.io/live/repository/icons/CertiFried-TechtonicaDirectConnect-1.0.11.png'
                    },
                    footer: {
                        text: 'Techtonica Dedicated Server • CertiFried',
                        icon_url: 'https://cdn.thunderstore.io/live/repository/icons/CertiFried-TechtonicaDirectConnect-1.0.11.png'
                    },
                    timestamp: new Date().toISOString()
                }]
            };

            const url = new URL(webhook.url);
            const reqModule = url.protocol === 'https:' ? https : http;

            const req = reqModule.request({
                hostname: url.hostname,
                port: url.port,
                path: url.pathname + url.search,
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });

            req.write(JSON.stringify(payload));
            req.end();
        } catch (err) {
            console.error(`Webhook error for ${webhook.name}:`, err.message);
        }
    }
}

// Start server
let server;

try {
    const sslOptions = {
        key: fs.readFileSync(CONFIG.sslKey),
        cert: fs.readFileSync(CONFIG.sslCert)
    };
    server = https.createServer(sslOptions, app);
    console.log('Running in HTTPS mode');
} catch (err) {
    console.log('Running in HTTP mode:', err.message);
    server = http.createServer(app);
}

// Socket.IO for real-time updates
const { Server } = require('socket.io');
const io = new Server(server, {
    path: '/socket.io',
    cors: { origin: '*' }
});

io.on('connection', (socket) => {
    console.log('Client connected');

    const statusInterval = setInterval(async () => {
        const serverStatus = await checkServerRunning();
        const systemStats = await getSystemStats();

        socket.emit('status', {
            server: {
                running: serverStatus.running,
                pid: serverStatus.pid,
                uptime: serverStatus.uptime,
                uptimeFormatted: formatUptime(serverStatus.uptime)
            },
            system: {
                memory: { ...systemStats.memory, totalFormatted: formatBytes(systemStats.memory.total), usedFormatted: formatBytes(systemStats.memory.used) },
                cpu: systemStats.cpu,
                disk: { ...systemStats.disk, totalFormatted: formatBytes(systemStats.disk.total), usedFormatted: formatBytes(systemStats.disk.used) }
            }
        });
    }, 5000);

    socket.on('disconnect', () => {
        console.log('Client disconnected');
        clearInterval(statusInterval);
    });
});

server.listen(CONFIG.port, '0.0.0.0', () => {
    console.log(`Techtonica Server Admin Panel running on http://0.0.0.0:${CONFIG.port}`);
    console.log(`Access via: https://certifriedmultitool.com${CONFIG.basePath}`);
    console.log(`Or: http://51.81.155.59:${CONFIG.port}`);
});
