<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Prototipo: Rol Jerárquico (RolPadre)</title>
    <style>
        :root {
            --primary-color: #1976d2;
            --primary-dark: #1565c0;
            --secondary-color: #ff9800;
            --success-color: #4caf50;
            --danger-color: #f44336;
            --warning-color: #ff9800;
            --info-color: #2196f3;
            --light-bg: #f5f5f5;
            --white: #ffffff;
            --text-dark: #212121;
            --text-light: #757575;
            --border-color: #e0e0e0;
            --shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
            --tree-indent: 24px;
        }

        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            line-height: 1.6;
            color: var(--text-dark);
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }

        .container {
            max-width: 1200px;
            margin: 0 auto;
            background: var(--white);
            border-radius: 12px;
            box-shadow: 0 10px 40px rgba(0, 0, 0, 0.15);
            overflow: hidden;
        }

        .header {
            background: linear-gradient(135deg, var(--primary-color), var(--secondary-color));
            color: var(--white);
            padding: 32px;
            text-align: center;
        }

        .header h1 {
            font-size: 2.5rem;
            margin-bottom: 8px;
            font-weight: 700;
        }

        .header p {
            font-size: 1.1rem;
            opacity: 0.95;
        }

        .nav {
            background: var(--light-bg);
            padding: 16px 32px;
            border-bottom: 1px solid var(--border-color);
            font-size: 0.9rem;
            color: var(--text-light);
        }

        .nav a {
            color: var(--primary-color);
            text-decoration: none;
            margin: 0 8px;
        }

        .nav a:hover {
            text-decoration: underline;
        }

        .content {
            padding: 32px;
        }

        .section {
            margin-bottom: 32px;
        }

        .section-title {
            font-size: 1.8rem;
            color: var(--primary-dark);
            margin-bottom: 16px;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .section-description {
            color: var(--text-light);
            margin-bottom: 24px;
            line-height: 1.7;
        }

        .grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 24px;
            margin-bottom: 32px;
        }

        @media (max-width: 768px) {
            .grid {
                grid-template-columns: 1fr;
            }
        }

        .card {
            background: var(--white);
            border: 1px solid var(--border-color);
            border-radius: 8px;
            padding: 20px;
            box-shadow: var(--shadow);
            transition: transform 0.2s;
        }

        .card:hover {
            transform: translateY(-2px);
        }

        .card-title {
            font-size: 1.3rem;
            color: var(--primary-dark);
            margin-bottom: 12px;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .tree-view {
            margin-top: 20px;
        }

        .tree-node {
            margin-bottom: 8px;
        }

        .tree-children {
            margin-left: var(--tree-indent);
        }

        .role-item {
            display: flex;
            align-items: center;
            padding: 12px;
            background: var(--light-bg);
            border-radius: 6px;
            margin-bottom: 8px;
            border-left: 4px solid var(--success-color);
            transition: all 0.3s ease;
        }

        .role-item:hover {
            background: #e3f2fd;
            box-shadow: 0 4px 12px rgba(33, 150, 243, 0.1);
        }

        .role-item.parent {
            border-left-color: var(--warning-color);
            background: rgba(255, 152, 0, 0.05);
        }

        .role-item.child {
            border-left-color: var(--info-color);
            background: rgba(33, 150, 243, 0.05);
        }

        .role-icon {
            width: 36px;
            height: 36px;
            border-radius: 50%;
            background: var(--success-color);
            color: var(--white);
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: bold;
            margin-right: 12px;
            flex-shrink: 0;
        }

        .role-info {
            flex: 1;
        }

        .role-name {
            font-weight: 600;
            margin-bottom: 4px;
            font-size: 1.1rem;
        }

        .role-meta {
            font-size: 0.85rem;
            color: var(--text-light);
            display: flex;
            gap: 16px;
            flex-wrap: wrap;
        }

        .role-badge {
            padding: 4px 10px;
            border-radius: 20px;
            font-size: 0.75rem;
            font-weight: 600;
            color: var(--white);
        }

        .badge-parent {
            background: var(--warning-color);
        }

        .badge-child {
            background: var(--info-color);
        }

        .role-permissions {
            margin-top: 8px;
            font-size: 0.85rem;
            color: var(--text-light);
        }

        .inheritance-info {
            background: rgba(76, 175, 80, 0.1);
            border-left: 4px solid var(--success-color);
            padding: 12px;
            border-radius: 4px;
            margin-top: 12px;
        }

        .inheritance-title {
            font-weight: 600;
            color: var(--success-color);
            margin-bottom: 8px;
            font-size: 0.95rem;
        }

        .permission-list {
            list-style: none;
            padding-left: 0;
        }

        .permission-list li {
            padding: 4px 0;
            padding-left: 20px;
            position: relative;
        }

        .permission-list li:before {
            content: "✓";
            position: absolute;
            left: 0;
            color: var(--success-color);
            font-weight: bold;
        }

        .role-actions {
            display: flex;
            gap: 8px;
            margin-top: 12px;
        }

        .btn {
            padding: 6px 12px;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 0.85rem;
            transition: all 0.2s;
            display: inline-flex;
            align-items: center;
            gap: 4px;
        }

        .btn-primary {
            background: var(--primary-color);
            color: var(--white);
        }

        .btn-primary:hover {
            background: var(--primary-dark);
            transform: translateY(-1px);
        }

        .btn-secondary {
            background: var(--light-bg);
            color: var(--text-dark);
            border: 1px solid var(--border-color);
        }

        .btn-secondary:hover {
            background: #e0e0e0;
        }

        .empty-state {
            text-align: center;
            padding: 40px;
            color: var(--text-light);
        }

        .controls {
            display: flex;
            gap: 12px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }

        .control-group {
            display: flex;
            align-items: center;
            gap: 8px;
            padding: 8px 12px;
            background: var(--light-bg);
            border-radius: 6px;
        }

        .control-label {
            font-size: 0.9rem;
            color: var(--text-light);
            font-weight: 500;
        }

        .control-input {
            padding: 6px 10px;
            border: 1px solid var(--border-color);
            border-radius: 4px;
            font-size: 0.9rem;
        }

        .highlight {
            background: rgba(255, 235, 59, 0.3);
            border-radius: 2px;
            padding: 0 4px;
        }

        .example-diagram {
            background: var(--light-bg);
            padding: 20px;
            border-radius: 8px;
            text-align: center;
            font-family: monospace;
            font-size: 0.9rem;
            overflow-x: auto;
            margin: 20px 0;
        }

        .flow {
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 16px;
            flex-wrap: wrap;
            margin: 16px 0;
        }

        .flow-item {
            padding: 12px 20px;
            background: var(--white);
            border-radius: 6px;
            border: 2px solid var(--primary-color);
            font-weight: 600;
        }

        .flow-arrow {
            color: var(--primary-color);
            font-size: 1.5rem;
        }

        .info-box {
            background: rgba(33, 150, 243, 0.1);
            border-left: 4px solid var(--info-color);
            padding: 16px;
            border-radius: 4px;
            margin: 16px 0;
        }

        .info-box-title {
            font-weight: 600;
            color: var(--info-color);
            margin-bottom: 8px;
        }

        .error-box {
            background: rgba(244, 67, 54, 0.1);
            border-left: 4px solid var(--danger-color);
            padding: 16px;
            border-radius: 4px;
            margin: 16px 0;
        }

        .error-box-title {
            font-weight: 600;
            color: var(--danger-color);
            margin-bottom: 8px;
        }

        .tips-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 16px;
            margin: 24px 0;
        }

        .tip-card {
            background: var(--light-bg);
            padding: 16px;
            border-radius: 6px;
            border-left: 4px solid var(--warning-color);
        }

        .tip-card h4 {
            color: var(--warning-color);
            margin-bottom: 8px;
            font-size: 1.1rem;
        }

        .tabs {
            display: flex;
            border-bottom: 2px solid var(--border-color);
            margin-bottom: 24px;
        }

        .tab {
            padding: 12px 24px;
            cursor: pointer;
            border: none;
            background: none;
            border-bottom: 2px solid transparent;
            font-size: 1rem;
            font-weight: 600;
            color: var(--text-light);
        }

        .tab.active {
            color: var(--primary-color);
            border-bottom-color: var(--primary-color);
        }

        .tab:hover {
            background: var(--light-bg);
        }

        .tab-content {
            display: none;
        }

        .tab-content.active {
            display: block;
        }

        .code-block {
            background: #f8f9fa;
            border: 1px solid #dee2e6;
            border-radius: 4px;
            padding: 16px;
            font-family: 'Courier New', monospace;
            font-size: 0.9rem;
            overflow-x: auto;
            margin: 16px 0;
        }

        .validation-rule {
            display: flex;
            align-items: center;
            gap: 8px;
            padding: 8px 12px;
            background: rgba(76, 175, 80, 0.1);
            border-radius: 4px;
            margin: 8px 0;
        }

        .validation-icon {
            color: var(--success-color);
            font-weight: bold;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>Rol Jerárquico (RolPadre)</h1>
            <p>Prototipo de UI/UX - Vista previa para <strong>Roles, Permisos y Accesos</strong></p>
        </div>

        <div class="nav">
            <a href="#">🏠 Panel Principal</a> |
            <a href="#">⚙️ Administración</a> |
            <a href="#">🔒 Roles y Permisos</a> |
            <a href="#">👥 Usuarios</a> |
            <a href="#">📊 Auditoría</a>
        </div>

        <div class="content">
            <div class="section">
                <h2 class="section-title">📋 Resumen Ejecutivo</h2>
                <p class="section-description">
                    La jerarquía de roles permite a los roles hijos <strong>heredar automáticamente todos los permisos de sus roles padres</strong>,
                    evitando la duplicación de asignaciones. Un rol <span class="highlight">"Supervisor"</span> puede heredar el rol <span class="highlight">"Operador"</span>
                    (todos sus permisos) + agregar permisos adicionales. La jerarquía es <strong>profunda</strong> (Soporte → Supervisor → Operador → Asistente) y <strong>plana</strong> (misma importancia de nivel).
                </p>

                <div class="grid">
                    <div class="card">
                        <h3 class="card-title">🔑 Función</h3>
                        <p>Evita la asignación manual y repetitiva de permisos a través de la jerarquía de roles.</p>
                        <ul style="margin-top: 12px; padding-left: 20px; font-size: 0.95rem;">
                            <li style="margin-bottom: 8px;">Permite que un rol herede permisos de un rol padre</li>
                            <li style="margin-bottom: 8px;">Simplifica la asignación de roles en múltiples niveles</li>
                            <li style="margin-bottom: 8px;">Permite gestión de permisos modular y escalable</li>
                            <li style="margin-bottom: 8px;">Reduce la propagación de errores en cambios de permisos</li>
                        </ul>
                    </div>

                    <div class="card">
                        <h3 class="card-title">🎯 Propósito</h3>
                        <p>Diseñado para organizaciones con muchas definiciones de roles, donde la asignación de permisos por rol requeriría más tiempo que tener un sistema de permisos.</p>
                        <ul style="margin-top: 12px; padding-left: 20px; font-size: 0.95rem;">
                            <li style="margin-bottom: 8px;">Organizaciones con muchos roles</li>
                            <li style="margin-bottom: 8px;">Organizaciones con roles complementarios</li>
                            <li style="margin-bottom: 8px;">Roles jerárquicos o por niveles</li>
                            <li style="margin-bottom: 8px;">Organizaciones que priorizan seguridad en jerarquía</li>
                        </ul>
                    </div>
                </div>
            </div>

            <div class="section">
                <h2 class="section-title">📊 ¿Qué es <code>RolJerarquico</code>?</h2>
                <p class="section-description">
                    <code>dbo.RolJerarquico</code> es una tabla de <strong>jerarquía (relación árbol</strong>)
                    donde un rol padre le asigna automáticamente sus permisos a un rol hijo.
                    Un rol puede ser padre de muchos hijos y hijo de muchos padres (polígono), 
                    pero no puede ser un ancestro de sí mismo (sin ciclos).
                </p>

                <div class="tree-view">
                    <div class="tree-node">
                        <div class="role-item parent">
                            <div class="role-icon">💼</div>
                            <div class="role-info">
                                <div class="role-name">Soporte <span class="role-badge badge-parent">PADRE</span></div>
                                <div class="role-meta">
                                    <span>📧 Módulo: Soporte</span>
                                    <span>🔑 12 Permisos</span>
                                    <span>👥 3 usuarios asignados</span>
                                </div>
                                <div class="role-permissions">
                                    Hereda: Ver Login, Crear Usuario, Eliminar Usuario
                                </div>
                            </div>
                        </div>

                        <div class="tree-children">
                            <div class="tree-node">
                                <div class="role-item parent">
                                    <div class="role-icon">👔</div>
                                    <div class="role-info">
                                        <div class="role-name">Supervisor <span class="role-badge badge-parent">PADRE</span></div>
                                        <div class="role-meta">
                                            <span>📊 Módulo: Supervisor</span>
                                            <span>🔑 8 Permisos</span>
                                            <span>👥 5 usuarios asignados</span>
                                        </div>
                                        <div class="role-permissions">
                                            Hereda: VER_DASHBOARD → VER_REPORTES, CONFIGURACION
                                        </div>
                                        <div class="inheritance-info">
                                            <div class="inheritance-title">▼ Permisos heredados del padre</div>
                                            <ul class="permission-list">
                                                <li>🔑 VER_LOGIN</li>
                                                <li>🔑 CREAR_USUARIO</li>
                                                <li>🔑 ELIMINAR_USUARIO</li>
                                                <li>🔑 VER_DASHBOARD</li>
                                                <li>🔑 VER_REPORTES</li>
                                                <li>🔑 CONFIGURACION</li>
                                                <li>🔑 AUDITORIA</li>
                                            </ul>
                                        </div>
                                        <div class="role-actions">
                                            <button class="btn btn-primary">✏️ Editar Jerarquía</button>
                                            <button class="btn btn-secondary">🗑️ Desactivar</button>
                                            <button class="btn btn-secondary">👥 Ver Usuarios</button>
                                        </div>
                                    </div>
                                </div>

                                <div class="tree-children">
                                    <div class="tree-node">
                                        <div class="role-item child">
                                            <div class="role-icon">👤</div>
                                            <div class="role-info">
                                                <div class="role-name">Operador <span class="role-badge badge-child">HIJO</span></div>
                                                <div class="role-meta">
                                                    <span>🔧 Módulo: Operador</span>
                                                    <span>🔑 5 Permisos</span>
                                                    <span>👥 15 usuarios asignados</span>
                                                </div>
                                                <div class="role-permissions">
                                                    Permisos propios: ReporteDeIncidencias, AtenderLlamados
                                                </div>
                                                <div class="inheritance-info">
                                                    <div class="inheritance-title">▼ Permisos heredados del padre</div>
                                                    <ul class="permission-list">
                                                        <li>🔑 VER_LOGIN</li>
                                                        <li>🔑 CREAR_USUARIO</li>
                                                        <li>🔑 ELIMINAR_USUARIO</li>
                                                        <li>🔑 VER_DASHBOARD</li>
                                                        <li>🔑 VER_REPORTES</li>
                                                        <li>🔑 CONFIGURACION</li>
                                                        <li>🔑 AUDITORIA</li>
                                                    </div>
                                                    <div class="inheritance-title">▼ Permisos propios</div>
                                                    <ul class="permission-list">
                                                        <li>📊 REPORT_INCIDENTES</li>
                                                        <li>📞 ATTEND_CALLS</li>
                                                    </ul>
                                                </div>
                                                <div class="role-actions">
                                                    <button class="btn btn-primary">✏️ Editar Jerarquía</button>
                                                    <button class="btn btn-secondary">👤 Asignar Usuario</button>
                                                </div>
                                            </div>
                                        </div>

                                        <div class="tree-children">
                                            <div class="tree-node">
                                                <div class="role-item child">
                                                    <div class="role-icon">📍</div>
                                                    <div class="role-info">
                                                        <div class="role-name">Asistente <span class="role-badge badge-child">HIJO</span></div>
                                                        <div class="role-meta">
                                                            <span>🎨 Módulo: Asistente</span>
                                                            <span>🔑 3 Permisos</span>
                                                            <span>👥 30 usuarios asignados</span>
                                                        </div>
                                                        <div class="role-permissions">
                                                            Permisos propios: GestionarChats
                                                        </div>
                                                        <div class="inheritance-info">
                                                            <div class="inheritance-title">▼ Permisos heredados del padre</div>
                                                            <ul class="permission-list">
                                                                <li>🔑 VER_LOGIN</li>
                                                                <li>🔑 CREAR_USUARIO</li>
                                                                <li>🔑 ELIMINAR_USUARIO</li>
                                                                <li>🔑 VER_DASHBOARD</li>
                                                                <li>🔑 VER_REPORTES</li>
                                                                <li>🔑 CONFIGURACION</li>
                                                                <li>🔑 AUDITORIA</li>
                                                            </div>
                                                            <div class="inheritance-title">▼ Permisos propios</div>
                                                            <ul class="permission-list">
                                                                <li>💬 MANAGE_CHATS</li>
                                                            </ul>
                                                        </div>
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <div class="section">
                <h2 class="section-title">🔍 Como Funciona</h2>

                <div class="info-box">
                    <div class="info-box-title">💡 Caso de Uso Principal</div>
                    <p>El equipo de Seguridad desea agregar un nuevo nivel de control: <strong>Solo administradores senior pueden ver auditorías.</strong></p>
                    <p><strong>Sin jerarquía:</strong> Deben agregar manualmente <code>AUDITORIA_LEER</code> al rol <code>Seguridad</code>, luego al rol <code>SeguridadSenior</code>, y al rol <code>SeguridadManager</code> — 3 operaciones.</p>
                    <p><strong>Con jerarquía:</strong> Solo necesitan crear rol <code>SeguridadSenior</code>, asignar permisos, luego establecer <code>SeguridadSenior</code> como hijo de <code>Seguridad</code>. <code>Seguridad</code> puede ser padre de <code>SeguridadSenior</code> y <code>SeguridadManager</code>. ¡Se soluciona automáticamente!</p>
                </div>

                <div class="example-diagram">
                    <strong>Diagrama de flujo jerárquico:</strong>
                    <div class="flow">
                        <div class="flow-item">SOPORTE</div>
                        <div class="flow-arrow">→</div>
                        <div class="flow-item">SUPERVISOR</div>
                        <div class="flow-arrow">→</div>
                        <div class="flow-item">OPERADOR</div>
                        <div class="flow-arrow">→</div>
                        <div class="flow-item">ASISTENTE</div>
                    </div>
                    <div style="margin-top: 16px; font-size: 0.9rem; color: var(--text-light);">
                        ↳ Permisos heredados (superset): Soporte → Supervisor → Operador → Asistente<br>
                        ↳ Permisos propios (subset): Supervisor → Operador → Asistente (cada uno agrega)
                    </div>
                </div>

                <h3 style="margin-top: 32px;">⚙️ Implementación</h3>
                <div class="code-block">
                    <!-- SQL para tabla RolJerarquico -->
                    CREATE TABLE dbo.RolJerarquico (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        IdRol INT NOT NULL,
                        IdRolPadre INT NULL,
                        Activo BIT NOT NULL DEFAULT 1,
                        CONSTRAINT FK_RolJerarquico_Rol FOREIGN KEY (IdRol)
                            REFERENCES dbo.Roles(Id) ON DELETE CASCADE,
                        CONSTRAINT FK_RolJerarquico_RolPadre FOREIGN KEY (IdRolPadre)
                            REFERENCES dbo.Roles(Id),
                        CONSTRAINT UQ_RolJerarquico UNIQUE (IdRol, IdRolPadre)
                    );
                    
                    CREATE UNIQUE INDEX IX_RolJerarquico_NoCiclo
                    ON dbo.RolJerarquico(IdRol, IdRolPadre)
                    WHERE IdRolPadre IS NOT NULL AND IdRol <> IdRolPadre;
                </div>

                <h3 style="margin-top: 32px;">🔍 Lógica de Consulta</h3>
                <div class="code-block">
                    WITH RecursiveRolHierarchy AS (
                        -- Nodos hoja (sin hijos)
                        SELECT IdRol, IdRolPadre
                        FROM dbo.RolJerarquico
                        WHERE IdRol NOT IN (SELECT IdRol FROM dbo.RolJerarquico WHERE IdRolPadre IS NOT NULL)
                        
                        UNION ALL
                        
                        -- Nodos padre (recursion hacia abajo)
                        SELECT r.IdRol, r.IdRolPadre
                        FROM dbo.RolJerarquico r
                        JOIN RecursiveRolHierarchy rh ON r.IdRol = rh.IdRol
                        WHERE r.Activo = 1 AND (rh.IdRolPadre IS NULL OR rh.IdRolPadre = r.IdRol)
                    )
                    SELECT DISTINCT rh.IdRol
                    FROM RecursiveRolHierarchy rh;
                </div>
            </div>

            <div class="section">
                <h2 class="section-title">🎨 Prototipo UI: Panel Jerárquico</h2>

                <div class="tabs">
                    <button class="tab active" onclick="showTab('view1')">Vista del árbol</button>
                    <button class="tab" onclick="showTab('view2')">Editor jerárquico</button>
                    <button class="tab" onclick="showTab('view3')">Mapa de permisos</button>
                </div>

                <div id="view1" class="tab-content active">
                    <h3>Vista del árbol jerárquico</h3>
                    <p>Visualización del árbol de roles con jerarquía padre-hijo.</p>
                    <div style="margin: 20px 0;">
                        <div style="display: flex; justify-content: center; padding: 40px;">
                            <div style="width: 300px; border: 2px solid var(--border-color); border-radius: 8px; padding: 20px;">
                                <div style="text-align: center; margin-bottom: 16px; font-weight: bold; color: var(--warning-color);">
                                    👔 SUPERVISOR <span style="font-size: 0.9rem; font-weight: normal;">(Padre)</span>
                                </div>
                                <div style="border-top: 1px solid var(--border-color); padding-top: 16px;">
                                    <div style="margin-bottom: 12px; padding-left: 20px; position: relative;">
                                        <div style="position: absolute; left: 0; top: 4px; width: 16px; height: 2px; background: var(--warning-color);"></div>
                                        <div style="padding: 8px 12px; background: rgba(255,152,0,0.1); border-radius: 4px; border-left: 3px solid var(--warning-color);">
                                            <div style="font-weight: 600; font-size: 0.95rem;">👤 OPERADOR</div>
                                            <div style="font-size: 0.85rem; color: var(--text-light); margin-top: 4px;">5 Permisos (3 propios + 7 heredados)</div>
                                        </div>
                                    </div>
                                    <div style="margin-bottom: 12px; padding-left: 20px; position: relative;">
                                        <div style="position: absolute; left: 0; top: 4px; width: 16px; height: 2px; background: var(--warning-color);"></div>
                                        <div style="padding: 8px 12px; background: rgba(33,150,243,0.1); border-radius: 4px; border-left: 3px solid var(--info-color);">
                                            <div style="font-weight: 600; font-size: 0.95rem;">📍 ASISTENTE</div>
                                            <div style="font-size: 0.85rem; color: var(--text-light); margin-top: 4px;">3 Permisos (1 propio + 7 heredados)</div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div id="view2" class="tab-content">
                    <h3>Editor de jerarquía</h3>
                    <p>Configurar relaciones padre-hijo entre roles.</p>

                    <div class="controls">
                        <div class="control-group">
                            <label class="control-label">Rol Hijo:</label>
                            <select class="control-input">
                                <option>Seleccione...</option>
                                <option>Operador</option>
                                <option>Asistente</option>
                                <option>Supervisor</option>
                            </select>
                        </div>
                        <div class="control-group">
                            <label class="control-label">Rol Padre:</label>
                            <select class="control-input">
                                <option>Ninguno (raíz)</option>
                                <option>Soporte</option>
                                <option>Supervisor</option>
                            </select>
                        </div>
                        <button class="btn btn-primary" style="align-self: center;">📐 Establecer Jerarquía</button>
                    </div>

                    <div class="info-box">
                        <div class="info-box-title">💡 Ejemplo: Herencia de permisos</div>
                        <ul style="margin-top: 8px; padding-left: 20px;">
                            <li>Padre <strong>Soporte</strong>: 12 permisos base (LOGIN, CREAR_USR, etc.)</li>
                            <li>Hijo <strong>Supervisor</strong>: Hereda 12 + agrega 3 = 15 total</li>
                            <li>Hijo <strong>Operador</strong>: Hereda 15 + agrega 2 = 17 total</li>
                            <li>Hijo <strong>Asistente</strong>: Hereda 17 + agrega 1 = 18 total</li>
                        </ul>
                    </div>
                </div>

                <div id="view3" class="tab-content">
                    <h3>Mapa de permisos por rol</h3>
                    <p>Visualización de permisos heredados vs propios.</p>

                    <table style="width: 100%; border-collapse: collapse; margin-top: 16px;">
                        <thead>
                            <tr style="border-bottom: 2px solid var(--border-color);">
                                <th style="padding: 12px; text-align: left;">Rol</th>
                                <th style="padding: 12px; text-align: center;">Permisos Totales</th>
                                <th style="padding: 12px; text-align: center;">Permisos Heredados</th>
                                <th style="padding: 12px; text-align: center;">Permisos Propios</th>
                                <th style="padding: 12px; text-align: center;">% Herencia</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr style="border-bottom: 1px solid var(--border-color);">
                                <td style="padding: 12px; font-weight: 600;">Soporte</td>
                                <td style="padding: 12px; text-align: center;">12</td>
                                <td style="padding: 12px; text-align: center;">0</td>
                                <td style="padding: 12px; text-align: center;">12</td>
                                <td style="padding: 12px; text-align: center;">0%</td>
                            </tr>
                            <tr style="border-bottom: 1px solid var(--border-color); background: rgba(255,152,0,0.05);">
                                <td style="padding: 12px; font-weight: 600; color: var(--warning-color);">
                                    👔 Supervisor <span style="font-size: 0.85rem;">(Padre)</span>
                                </td>
                                <td style="padding: 12px; text-align: center; font-weight: 600;">15</td>
                                <td style="padding: 12px; text-align: center;">0</td>
                                <td style="padding: 12px; text-align: center;">15</td>
                                <td style="padding: 12px; text-align: center;">0%</td>
                            </tr>
                            <tr style="border-bottom: 1px solid var(--border-color); background: rgba(33,150,243,0.05);">
                                <td style="padding: 12px; font-weight: 600; color: var(--info-color);">
                                    👤 Operador <span style="font-size: 0.85rem;">(Hijo de Supervisor)</span>
                                </td>
                                <td style="padding: 12px; text-align: center; font-weight: 600;">17</td>
                                <td style="padding: 12px; text-align: center;">15</td>
                                <td style="padding: 12px; text-align: center;">2</td>
                                <td style="padding: 12px; text-align: center;">88%</td>
                            </tr>
                            <tr style="border-bottom: 1px solid var(--border-color); background: rgba(33,150,243,0.05);">
                                <td style="padding: 12px; font-weight: 600; color: var(--info-color);">
                                    📍 Asistente <span style="font-size: 0.85rem;">(Hijo de Operador)</span>
                                </td>
                                <td style="padding: 12px; text-align: center; font-weight: 600;">18</td>
                                <td style="padding: 12px; text-align: center;">17</td>
                                <td style="padding: 12px; text-align: center;">1</td>
                                <td style="padding: 12px; text-align: center;">94%</td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </div>

            <div class="section">
                <h2 class="section-title">🎯 Validaciones Clave</h2>

                <div class="validation-rule">
                    <span class="validation-icon">✓</span>
                    <span><strong>Sin ciclos:</strong> Un rol no puede ser padre de sí mismo (IdRol ≠ IdRolPadre)</span>
                </div>

                <div class="validation-rule">
                    <span class="validation-icon">✓</span>
                    <span><strong>Sin duplicados:</strong> Un rol padre no puede ser asignado al mismo rol (IdRol, IdRolPadre UNIQUE)</span>
                </div>

                <div class="validation-rule">
                    <span class="validation-icon">✓</span>
                    <span><strong>Árbol profundo:</strong> Permite herencia anidada: A → B → C → D</span>
                </div>

                <div class="validation-rule">
                    <span class="validation-icon">✓</span>
                    <span><strong>Jerarquía poligonal:</strong> Un rol puede tener múltiples hijos y ser hijo de múltiples padres</span>
                </div>

                <div class="error-box">
                    <div class="error-box-title">🚫 Prohibiciones</div>
                    <ul style="margin-top: 8px; padding-left: 20px;">
                        <li style="margin-bottom: 8px;">Un rol padre no puede ser un ancestro lejano de sí mismo (detectado con CTE)</li>
                        <li style="margin-bottom: 8px;">Un rol con asignaciones activas no puede ser eliminado (FK en la tabla)</li>
                        <li style="margin-bottom: 8px;">No se permite la asignación de jerarquía en la UI si los dos roles tienen el mismo ID (auto-protección)</li>
                    </ul>
                </div>
            </div>

            <div class="section">
                <h2 class="section-title">💡 Tips de Implementación</h2>

                <div class="tips-grid">
                    <div class="tip-card">
                        <h4>🎯 Caso de Uso Común</h4>
                        <p><strong>Escenario:</strong> Un equipo de seguridad desea restringir la auditoría solo a administradores senior.</p>
                        <p><strong>Solución:</strong> Crear rol <code>Seguridad</code> → <code>SeguridadSenior</code> → <code>SeguridadManager</code>. Asignar permisos a <code>SeguridadSenior</code>, establecer <code>SeguridadSenior</code> como hijo de <code>Seguridad</code> y <code>SeguridadManager</code> como hijo de <code>SeguridadSenior</code>.</p>
                    </div>

                    <div class="tip-card">
                        <h4>🔧 Integración API</h4>
                        <p>Agregar nuevo endpoint: <code>POST /api/roles/{idRol}/jerarquia</code> para asignar/desasignar relaciones padre-hijo.</p>
                        <p>La API retorna toda la jerarquía con la información de permisos heredados calculada en tiempo real.</p>
                    </div>

                    <div class="tip-card">
                        <h4>📊 UI/UX</h4>
                        <p>Mostrar el árbol en forma de acordeón expandible, con contadores de permisos por nodo.</p>
                        <p>Permitir drag-and-drop visual para una experiencia más intuitiva de establecer jerarquía.</p>
                    </div>

                    <div class="tip-card">
                        <h4>⚡ Optimización de Base de Datos</h4>
                        <p>Indexar columnas: (<code>IdRol</code>, <code>IdRolPadre</code>) para búsquedas rápidas.</p>
                        <p>Usar CTE recuso para calcular toda la jerarquía del árbol en una sola consulta.</p>
                    </div>
                </div>
            </div>

            <div class="section">
                <h2 class="section-title">📋 Checklist de Implementación</h2>

                <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 16px;">
                    <div style="background: var(--light-bg); padding: 16px; border-radius: 6px; border-left: 4px solid var(--success-color);">
                        <h4 style="color: var(--success-color); margin-bottom: 12px;">✅ Data Layer</h4>
                        <ul style="padding-left: 20px; font-size: 0.95rem;">
                            <li style="margin-bottom: 6px;">Crear tabla <code>RolJerarquico</code></li>
                            <li style="margin-bottom: 6px;">Agregar FK a <code>Roles(IdRol)</code> y <code>Roles(IdRolPadre)</code></li>
                            <li style="margin-bottom: 6px;">Crear índice único (<code>IdRol</code>, <code>IdRolPadre</code>)</li>
                            <li style="margin-bottom: 6px;">Agregar FK de cascada</li>
                        </ul>
                    </div>

                    <div style="background: var(--light-bg); padding: 16px; border-radius: 6px; border-left: 4px solid var(--primary-color);">
                        <h4 style="color: var(--primary-color); margin-bottom: 12px;">🔧 Servicios API</h4>
                        <ul style="padding-left: 20px; font-size: 0.95rem;">
                            <li style="margin-bottom: 6px;">Agregar método <code>SetJerarquiaAsync(int idRol, int? idRolPadre)</code></li>
                            <li style="margin-bottom: 6px;">Agregar método <code>GetJerarquiaAsync(int idRol)</code> para obtener árbol completo</li>
                            <li style="margin-bottom: 6px;">Incluir campos <code>PermisosHeritados</code>, <code>PermisosPropios</code> en DTO</li>
                        </ul>
                    </div>

                    <div style="background: var(--light-bg); padding: 16px; border-radius: 6px; border-left: 4px solid var(--warning-color);">
                        <h4 style="color: var(--warning-color); margin-bottom: 12px;">🎨 UI</h4>
                        <ul style="padding-left: 20px; font-size: 0.95rem;">
                            <li style="margin-bottom: 6px;">Agregar pestaña "Jerarquía" en vista de detalle de rol</li>
                            <li style="margin-bottom: 6px;">Componente <code>RoleTreeSelector</code> para elegir padre/hijo</li>
                            <li style="margin-bottom: 6px;">Visualización de árbol jerárquico en modo solo lectura</li>
                            <li style="margin-bottom: 6px;">Editor modal con vista previa de permisos heredados</li>
                        </ul>
                    </div>

                    <div style="background: var(--light-bg); padding: 16px; border-radius: 6px; border-left: 4px solid var(--info-color);">
                        <h4 style="color: var(--info-color); margin-bottom: 12px;">📊 Funcionalidades</h4>
                        <ul style="padding-left: 20px; font-size: 0.95rem;">
                            <li style="margin-bottom: 6px;">Calcular permisos heredados en tiempo real</li>
                            <li style="margin-bottom: 6px;">Validar ciclos antes de guardar</li>
                            <li style="margin-bottom: 6px;">Permitir múltiples padres/hijos</li>
                            <li style="margin-bottom: 6px;">Mostrar estado jerárquico en tabla de roles</li>
                        </ul>
                    </div>
                </div>
            </div>

            <div class="section">
                <h2 class="section-title">🏆 Beneficios de la Jerarquía de Roles</h2>

                <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 20px;">
                    <div style="background: linear-gradient(135deg, rgba(76, 175, 80, 0.1), rgba(139, 195, 74, 0.1)); padding: 20px; border-radius: 8px; border-left: 4px solid var(--success-color);">
                        <h3 style="color: var(--success-color); margin-bottom: 12px;">⚡ Eficiencia</h3>
                        <p style="font-size: 0.95rem; line-height: 1.6;">Una asignación para múltiples roles: Los roles hijos heredan automáticamente todas las asignaciones de sus padres, eliminando la necesidad de copiar permisos repetidamente.</p>
                    </div>

                    <div style="background: linear-gradient(135deg, rgba(33, 150, 243, 0.1), rgba(100, 181, 246, 0.1)); padding: 20px; border-radius: 8px; border-left: 4px solid var(--info-color);">
                        <h3 style="color: var(--info-color); margin-bottom: 12px;">🛡️ Seguridad</h3>
                        <p style="font-size: 0.95rem; line-height: 1.6;">Prevención de duplicación de permisos: Reduce el riesgo de asignaciones inconsistentes que ocurren cuando los administradores asignan manualmente los mismos permisos a múltiples roles.</p>
                    </div>

                    <div style="background: linear-gradient(135deg, rgba(255, 152, 0, 0.1), rgba(255, 204, 102, 0.1)); padding: 20px; border-radius: 8px; border-left: 4px solid var(--warning-color);">
                        <h3 style="color: var(--warning-color); margin-bottom: 12px;">📈 Escalabilidad</h3>
                        <p style="font-size: 0.95rem; line-height: 1.6;">Jerarquía profunda: Soporta más de 10 niveles de roles sin sobrecarga operacional. Perfecto para grandes organizaciones con complejas estructuras de autorización.</p>
                    </div>

                    <div style="background: linear-gradient(135deg, rgba(156, 39, 176, 0.1), rgba(186, 104, 200, 0.1)); padding: 20px; border-radius: 8px; border-left: 4px solid #9c27b0;">
                        <h3 style="color: #9c27b0; margin-bottom: 12px;">🔍 Auditoría y Compliance</h3>
                        <p style="font-size: 0.95rem; line-height: 1.6;">Historial claro de herencia: Cada cambio de jerarquía se audita, mostrando qué rol hereda de qué, facilitando cumplimiento y soluciones forenses.</p>
                    </div>
                </div>
            </div>
        </div>

        <script>
            function showTab(tabId) {
                // Hide all tabs
                const tabs = document.querySelectorAll('.tab-content');
                tabs.forEach(tab => tab.classList.remove('active'));
                
                // Remove active class from all buttons
                const buttons = document.querySelectorAll('.tab');
                buttons.forEach(btn => btn.classList.remove('active'));
                
                // Show selected tab
                document.getElementById(tabId).classList.add('active');
                
                // Add active class to clicked button
                event.target.classList.add('active');
            }
        </script>
    </div>
</body>
</html>
