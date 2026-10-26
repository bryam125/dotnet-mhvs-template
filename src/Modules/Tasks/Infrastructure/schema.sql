-- Tabla del módulo Tasks (HU-TASKS-001), usada por MySqlTaskRepository.
--
-- SOLO DOCUMENTACIÓN DE REFERENCIA. Esta tabla se crea manualmente, una sola vez, contra la
-- instancia real de MySQL. La aplicación nunca la crea ni la migra: este archivo no se ejecuta
-- desde el código.
--
-- created_at y updated_at guardan instantes UTC. DATETIME (a diferencia de TIMESTAMP) no aplica
-- ninguna conversión por la zona horaria de la sesión.

CREATE TABLE tasks (
  id CHAR(36) NOT NULL PRIMARY KEY,
  title VARCHAR(120) NOT NULL,
  description VARCHAR(2000) NULL,
  due_date DATE NULL,
  status VARCHAR(20) NOT NULL,
  created_at DATETIME(6) NOT NULL,
  updated_at DATETIME(6) NOT NULL
);
