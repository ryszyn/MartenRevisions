-- Database: martendocrevisions

-- DROP DATABASE IF EXISTS martendocrevisions;

CREATE DATABASE martendocrevisions
    WITH
    OWNER = postgres
    ENCODING = 'UTF8'
    LC_COLLATE = 'Polish_Poland.1250'
    LC_CTYPE = 'Polish_Poland.1250'
    LOCALE_PROVIDER = 'libc'
    TABLESPACE = pg_default
    CONNECTION LIMIT = -1
    IS_TEMPLATE = False;

-- Table: public.mt_doc_dbentity

-- DROP TABLE IF EXISTS public.mt_doc_dbentity;

CREATE TABLE IF NOT EXISTS public.mt_doc_dbentity
(
    id uuid NOT NULL,
    data jsonb NOT NULL,
    mt_last_modified timestamp with time zone DEFAULT transaction_timestamp(),
    mt_dotnet_type character varying COLLATE pg_catalog."default",
    mt_version integer NOT NULL DEFAULT 0,
    CONSTRAINT pkey_mt_doc_dbentity_id PRIMARY KEY (id)
)

TABLESPACE pg_default;

ALTER TABLE IF EXISTS public.mt_doc_dbentity
    OWNER to postgres;