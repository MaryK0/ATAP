"""
Миграция данных из PostgreSQL в MongoDB (пакетная, с чекпоинтом).
Запуск: C:\python_13\python.exe migrate_to_mongo.py

При обрыве — запусти повторно, продолжит с места остановки.
Требования: pip install psycopg2-binary pymongo
"""

import sys
sys.stdout.reconfigure(encoding='utf-8')

import json
import os
import psycopg2
from pymongo import MongoClient

PG_HOST     = "localhost"
PG_PORT     = 5433
PG_DB       = "atap_db"
PG_USER     = "postgres"
PG_PASSWORD = "mysecretpassword"

MONGO_URI   = "mongodb://localhost:27017"
MONGO_DB    = "atap_db"

BATCH_SIZE       = 10000
CHECKPOINT_FILE  = "migrate_checkpoint.json"


def get_pg_conn():
    return psycopg2.connect(
        host=PG_HOST, port=PG_PORT, dbname=PG_DB,
        user=PG_USER, password=PG_PASSWORD
    )


def load_checkpoint():
    if os.path.exists(CHECKPOINT_FILE):
        with open(CHECKPOINT_FILE, "r") as f:
            return json.load(f)
    return {}


def save_checkpoint(cp):
    with open(CHECKPOINT_FILE, "w") as f:
        json.dump(cp, f)


def migrate_batch(name, pg_cur, mongo_db, cp, query, mapper, index_fn=None):
    """Универсальная пакетная миграция одной коллекции."""
    if cp.get(name) == "done":
        print(f"  {name}: уже выполнено, пропуск")
        return

    offset = cp.get(f"{name}_offset", 0)

    if offset == 0:
        mongo_db.drop_collection(name)
        if index_fn:
            index_fn(mongo_db[name])

    total = 0
    while True:
        pg_cur.execute(query + f" LIMIT {BATCH_SIZE} OFFSET {offset}")
        rows = pg_cur.fetchall()
        if not rows:
            break

        docs = [mapper(r) for r in rows]
        mongo_db[name].insert_many(docs, ordered=False)

        offset += len(rows)
        total  += len(rows)
        cp[f"{name}_offset"] = offset
        save_checkpoint(cp)
        print(f"    {name}: вставлено {total} записей...", end="\r")

    cp[name] = "done"
    cp.pop(f"{name}_offset", None)
    save_checkpoint(cp)
    print(f"  {name}: готово ({total} записей)          ")


def migrate_users(pg_cur, mongo_db, cp):
    print("Миграция users...")

    if cp.get("users") == "done":
        print("  users: уже выполнено, пропуск")
        return

    pg_cur.execute("SELECT user_id, username, email, password_hash, role, created_at FROM dds.users ORDER BY user_id")
    rows = pg_cur.fetchall()

    mongo_db.drop_collection("users")
    if rows:
        docs = [{"userId": r[0], "username": r[1], "email": r[2],
                 "passwordHash": r[3], "role": r[4], "createdAt": r[5]} for r in rows]
        mongo_db["users"].insert_many(docs)
        mongo_db["users"].create_index("username", unique=True)
        mongo_db["users"].create_index("email",    unique=True)
        mongo_db["users"].create_index("userId",   unique=True)

        max_id = max(r[0] for r in rows)
        mongo_db["counters"].delete_one({"_id": "users"})
        mongo_db["counters"].insert_one({"_id": "users", "seq": max_id})

    cp["users"] = "done"
    save_checkpoint(cp)
    print(f"  users: готово ({len(rows)} записей)")


def migrate_airports(pg_cur, mongo_db, cp):
    print("Миграция airports...")
    migrate_batch(
        "airports", pg_cur, mongo_db, cp,
        query="SELECT airport_id, icao_code, name, latitude, longitude FROM dds.airports ORDER BY airport_id",
        mapper=lambda r: {"airportId": r[0], "icaoCode": r[1], "name": r[2],
                          "latitude": float(r[3]), "longitude": float(r[4])},
        index_fn=lambda col: col.create_index("airportId", unique=True)
    )


def migrate_current_flights(pg_cur, mongo_db, cp):
    print("Миграция current_flights...")
    migrate_batch(
        "current_flights", pg_cur, mongo_db, cp,
        query="SELECT flight_id, callsign, latitude, longitude, altitude FROM mart.current_flights_map ORDER BY flight_id",
        mapper=lambda r: {"flightId": r[0], "callsign": r[1] or "",
                          "latitude": float(r[2]) if r[2] else 0.0,
                          "longitude": float(r[3]) if r[3] else 0.0,
                          "altitude": int(r[4]) if r[4] else 0},
        index_fn=lambda col: col.create_index("flightId")
    )


def migrate_congestion(pg_cur, mongo_db, cp):
    print("Миграция mart_airport_congestion...")
    migrate_batch(
        "mart_airport_congestion", pg_cur, mongo_db, cp,
        query="""SELECT id, airport_id, flight_id, callsign,
                        entry_time, exit_time, duration_seconds, date_hour,
                        is_departure, is_arrival,
                        max_altitude_in_zone, min_altitude_in_zone, loaded_at
                 FROM mart.mart_airport_congestion ORDER BY id""",
        mapper=lambda r: {"_id": int(r[0]), "airportId": r[1], "flightId": r[2],
                          "callsign": r[3] or "", "entryTime": r[4], "exitTime": r[5],
                          "durationSeconds": r[6] or 0, "dateHour": r[7],
                          "isDeparture": bool(r[8]), "isArrival": bool(r[9]),
                          "maxAltitudeInZone": r[10] or 0, "minAltitudeInZone": r[11] or 0,
                          "loadedAt": r[12]},
        index_fn=lambda col: (col.create_index("airportId"), col.create_index("entryTime"))
    )


def migrate_saved_reports(pg_cur, mongo_db, cp):
    print("Миграция saved_reports...")
    migrate_batch(
        "saved_reports", pg_cur, mongo_db, cp,
        query="SELECT id, user_id, airport_id, name, date_from, date_to, created_at FROM dds.saved_reports ORDER BY id",
        mapper=lambda r: {"userId": r[1], "airportId": r[2], "name": r[3],
                          "dateFrom": r[4].isoformat(), "dateTo": r[5].isoformat(),
                          "createdAt": r[6]},
        index_fn=lambda col: col.create_index("userId")
    )


def main():
    cp = load_checkpoint()
    if cp:
        print("=== Продолжение миграции с чекпоинта ===\n")
    else:
        print("=== Миграция PostgreSQL -> MongoDB ===\n")

    pg_conn  = get_pg_conn()
    pg_cur   = pg_conn.cursor()
    mongo_db = MongoClient(MONGO_URI)[MONGO_DB]

    try:
        migrate_users(pg_cur, mongo_db, cp)
        migrate_airports(pg_cur, mongo_db, cp)
        migrate_current_flights(pg_cur, mongo_db, cp)
        migrate_congestion(pg_cur, mongo_db, cp)
        migrate_saved_reports(pg_cur, mongo_db, cp)

        # Чекпоинт больше не нужен
        if os.path.exists(CHECKPOINT_FILE):
            os.remove(CHECKPOINT_FILE)

        print("\nМиграция завершена успешно")
    except Exception as e:
        print(f"\nОшибка: {e}. Чекпоинт сохранён — запусти скрипт повторно для продолжения.")
        raise
    finally:
        pg_cur.close()
        pg_conn.close()


if __name__ == "__main__":
    main()
