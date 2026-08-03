import Database from "better-sqlite3";
import * as path from "path";
import * as fs from "node:fs";
import {
    Dependencies,
    DI,
    System,
    TYPES
} from "@criticalmanufacturing/connect-iot-controller-engine";

@DI.Injectable()
export class SQLiteManager {

    private _db;
    private _dbLocation: string;
    private _tableSchemas: Set<string>;

    @DI.Inject(TYPES.Dependencies.Logger)
    private _logger: Dependencies.Logger;

    @DI.Inject(TYPES.System.PersistedDataStore)
    private _dataStore: System.DataStore;

    public startSQLLite(dbPath = "iot.db", cleanupTimerInterval = 60000) {
        const persistencyLocation = path.join(this._dataStore["_handler"]["_config"]["path"], "SQLite", this._dataStore["_controllerId"].replace("/", "_"));

        fs.mkdirSync(persistencyLocation, { recursive: true });
        this._dbLocation = path.join(persistencyLocation, dbPath);
        this._logger.debug(`Started SQLite DB at ${this._dbLocation}`);

        this._db = new Database(this._dbLocation);
        this._tableSchemas = new Set(); // Track created tables

        this.scheduleCleanup(cleanupTimerInterval);
    }

    public insert(id: string, documentType: string, data: object, ttlSeconds?: number) {

        const tableName = this.guardClause(documentType);

        if (id == null || id === "") {
            id = this.newGuid();
        }

        // Check if data already exists
        const checkStmt = this._db.prepare(`
            SELECT id FROM ${tableName} WHERE json(data) = json(?)
        `);
        const existingRow = checkStmt.get(JSON.stringify(data));

        if (existingRow) {
            // Update existing row with new TTL
            const updateStmt = this._db.prepare(`
                UPDATE ${tableName} 
                SET updated_at = CURRENT_TIMESTAMP,
                    expires_at = CASE 
                        WHEN ? IS NOT NULL THEN datetime(CURRENT_TIMESTAMP, '+' || ? || ' seconds')
                        ELSE NULL 
                    END
                WHERE id = ?
            `);

            updateStmt.run(ttlSeconds, ttlSeconds, existingRow.id);

            return { id: existingRow.id, result: { changes: 1 }, existed: true };
        }

        // Insert new row with calculated expires_at
        const stmt = this._db.prepare(`
            INSERT INTO ${tableName} (id, data, created_at, updated_at, expires_at) 
            VALUES (?, json(json_extract(?, '$')), CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 
                    CASE 
                        WHEN ? IS NOT NULL THEN datetime(CURRENT_TIMESTAMP, '+' || ? || ' seconds')
                        ELSE NULL 
                    END)
        `);

        return {
            id,
            result: stmt.run(id, JSON.stringify(data), ttlSeconds, ttlSeconds),
            existed: false
        };
    }

    public queryByType(documentType: string, limit = 100) {
        try {
            const tableName = this.guardClause(documentType);

            // Check if table exists
            if (!this.tableExists(tableName)) {
                return [];
            }

            const stmt = this._db.prepare(`
                SELECT id, json_extract(data, '$') as data, created_at
                FROM ${tableName}
                ORDER BY created_at DESC
                LIMIT ?
            `);
            const rows = stmt.all(limit);
            return rows.map((row: { data: string; }) => ({ ...row, data: JSON.parse(row.data) }));
        } catch (error) {
            this._logger.error("Error querying by type:", error);
            return [];
        }
    }

    // Dynamic JSON path queries - find documents with specific values
    public queryByPath(documentType: string, jsonPath: string, value: string) {
        try {
            const tableName = this.guardClause(documentType);

            const query = `
          SELECT id, json_extract(data, '$') as data, created_at, updated_at
          FROM ${tableName} 
          WHERE json_extract(data, ?) = ?
        `;
            const params = ["$." + jsonPath, value];

            const stmt = this._db.prepare(query);
            const rows = stmt.all(...params);
            return rows.map((row: { data: string; }) => ({
                ...row,
                data: JSON.parse(row.data)
            }));
        } catch (error) {
            this._logger.error("Error querying by path:", error);
            return [];
        }
    }

    // Find documents containing specific nested values
    public queryNestedArray(documentType: string, arrayPath: string, itemField: string, itemValue: string) {
        try {
            const tableName = this.guardClause(documentType);

            // Query arrays using json_each
            const stmt = this._db.prepare(`
                SELECT DISTINCT d.id, json_extract(d.data, '$') as data,
                        d.created_at, d.updated_at
                FROM ${tableName} d,
                    json_each(d.data, ?) as item
                WHERE json_extract(item.value, ?) = ?
            `);

            const rows = stmt.all(arrayPath, `$.${itemField}`, itemValue);
            return rows.map((row: { data: string; }) => ({
                ...row,
                data: JSON.parse(row.data)
            }));
        } catch (error) {
            this._logger.error("Error querying nested array:", error);
            return [];
        }
    }

    // Flexible search with multiple criteria
    public flexibleSearch(documentType: string,
        criteria: {
            jsonFilters: { operator: string; path: string; value: any }[];
            limit: any;
        }) {
        try {

            const tableName = this.guardClause(documentType);

            let query = `
                SELECT id, json_extract(data, '$') as data,
                    created_at, updated_at
                FROM ${tableName} WHERE 1=1
            `;
            const params = [];

            // Add JSON path filters
            if (criteria.jsonFilters) {
                criteria.jsonFilters.forEach(filter => {
                    filter.path = `$.${filter.path}`;
                    switch (filter.operator) {
                        case "equals":
                            query += ` AND json_extract(data, ?) = ?`;
                            params.push(filter.path, filter.value);
                            break;
                        case "greater_than":
                            query += ` AND CAST(json_extract(data, ?) AS REAL) > ?`;
                            params.push(filter.path, filter.value);
                            break;
                        case "less_than":
                            query += ` AND CAST(json_extract(data, ?) AS REAL) < ?`;
                            params.push(filter.path, filter.value);
                            break;
                        case "contains":
                            query += ` AND json_extract(data, ?) LIKE ?`;
                            params.push(filter.path, `%${filter.value}%`);
                            break;
                    }
                });
            }

            // Add ordering and limit
            query += ` ORDER BY created_at DESC`;
            if (criteria.limit) {
                query += ` LIMIT ?`;
                params.push(criteria.limit);
            }

            const stmt = this._db.prepare(query);
            const rows = stmt.all(...params);
            return rows.map(row => ({
                ...row,
                data: JSON.parse(row.data)
            }));
        } catch (error) {
            this._logger.error("Error in flexible search:", error);
            return [];
        }
    }

    // Raw Query search
    public rawQuery(query: string, params: string[]) {
        if (this._db == null) {
            this.startSQLLite();
        }

        const stmt = this._db.prepare(query, params);
        const rows = stmt.all(...params);

        return rows;
    }

    // Get all document types (table names)
    public getDocumentTypes() {

        this.guardClause();

        const stmt = this._db.prepare(`
        SELECT name as table_name
        FROM sqlite_master 
        WHERE type='table' AND name != 'sqlite_sequence'
        ORDER BY name
      `);
        return stmt.all().map(row => row.table_name);
    }

    // Analytics require querying multiple tables
    public getDocumentTypeStats() {

        this.guardClause();

        const types = this.getDocumentTypes();
        const stats = [];

        types.forEach(tableName => {
            try {
                const stmt = this._db.prepare(`
            SELECT COUNT(*) as count,
                   MIN(created_at) as oldest,
                   MAX(created_at) as newest
            FROM ${tableName}
          `);
                const result = stmt.get();
                stats.push({
                    document_type: tableName,
                    ...result
                });
            } catch (error) {
                console.warn(`Error getting stats for ${tableName}:`, error.message);
            }
        });

        return stats.sort((a, b) => b.count - a.count);
    }

    public tableExists(tableName: string) {
        tableName = this.guardClause(tableName);

        const stmt = this._db.prepare(`
        SELECT name FROM sqlite_master 
        WHERE type='table' AND name=?
      `);
        return !!stmt.get(tableName);
    }

    // Create table for specific document type
    private ensureTable(documentType: string) {
        const tableName = this.sanitizeTableName(documentType);

        if (!this._tableSchemas.has(tableName)) {
            this._db.exec(`
            CREATE TABLE IF NOT EXISTS ${tableName} (
                id TEXT PRIMARY KEY,
                data JSON NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                expires_at DATETIME DEFAULT NULL
            )
            `);

            // Type-specific indexes can be optimized
            this._db.exec(`CREATE INDEX IF NOT EXISTS idx_${tableName}_created_at ON ${tableName}(created_at);`);
            this._db.exec(`CREATE INDEX IF NOT EXISTS idx_${tableName}_expires_at ON ${tableName}(expires_at)`);

            this._tableSchemas.add(tableName);
        }

        return tableName;
    }

    private guardClause(documentType: string = null): string {
        if (this._db == null) {
            this.startSQLLite();
        }

        if (documentType != null) {
            return this.ensureTable(documentType);
        }
        return null;
    }

    // Clean up expired documents
    private cleanupExpired(documentType: string): number {
        const tableName = this.ensureTable(documentType);

        const stmt = this._db.prepare(`
        DELETE FROM ${tableName} 
        WHERE expires_at IS NOT NULL AND expires_at <= datetime('now')
    `);

        const result = stmt.run();
        return result.changes;
    }

    // Auto-cleanup scheduler (call this periodically)
    private scheduleCleanup(intervalMs: number = 60000) {
        setInterval(() => {
            try {
                this._tableSchemas.forEach(table => {
                    const cleaned = this.cleanupExpired(table);
                    if (cleaned > 0) {
                        this._logger.debug(`Cleaned up ${cleaned} expired documents from ${table}`);
                    }
                });
            } catch (error) {
                this._logger.error("Error during TTL cleanup:", error);
            }
        }, intervalMs);
    }

    private sanitizeTableName(documentType: string) {
        // Convert document type to valid table name
        return documentType
            .replace(/[^a-zA-Z0-9_]/g, "_")
            .replace(/^[0-9]/, "_$&") // Prefix if starts with number
            .toLowerCase();
    }

    private close() {
        this._db.close();
    }

    private newGuid() {
        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (c) {
            const r = Math.random() * 16 | 0,
                // tslint:disable-next-line:triple-equals
                v = c === "x" ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }
}