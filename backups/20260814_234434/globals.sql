--
-- PostgreSQL database cluster dump
--

SET default_transaction_read_only = off;

SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;

--
-- Roles
--

CREATE ROLE franco;
ALTER ROLE franco WITH SUPERUSER INHERIT NOCREATEROLE CREATEDB LOGIN NOREPLICATION NOBYPASSRLS PASSWORD 'SCRAM-SHA-256$4096:3uthujoQKlVC1Qxd7xGyOA==$1nEAzThbBJbzgVJjYBNXE5pZAvelpzDeWOYq4taXKM8=:H5xxz75dTYzNt39BZfV1+J/kKW1jf0gALP4H+AMVo2E=';
CREATE ROLE postgres;
ALTER ROLE postgres WITH SUPERUSER INHERIT CREATEROLE CREATEDB LOGIN REPLICATION BYPASSRLS PASSWORD 'SCRAM-SHA-256$4096:0ho0M1RKvJFbeSvwOFBKOQ==$Ck4POWtzjGLPu3pDmPM7kXZNYReLqv6O0iAC6ulLeXk=:lN/sj5XmPWeAGKlEEf1Kkie8ceqKAM/RBFkY5d7i6Ps=';

--
-- User Configurations
--








--
-- PostgreSQL database cluster dump complete
--

