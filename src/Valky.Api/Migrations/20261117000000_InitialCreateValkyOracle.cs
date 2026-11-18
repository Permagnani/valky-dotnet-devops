using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Valky.Api.Migrations;

/// <inheritdoc />
public partial class InitialCreateValkyOracle : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TB_VALKY_ALERTAS",
            columns: table => new
            {
                ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                    .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                CODIGO_BO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                LATITUDE = table.Column<decimal>(type: "DECIMAL(9,6)", precision: 9, scale: 6, nullable: false),
                LONGITUDE = table.Column<decimal>(type: "DECIMAL(9,6)", precision: 9, scale: 6, nullable: false),
                STATUS = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                CRIADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                ATUALIZADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TB_VALKY_ALERTAS", x => x.ID);
            });

        migrationBuilder.CreateTable(
            name: "TB_VALKY_USUARIOS",
            columns: table => new
            {
                ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                    .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                NOME = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                CPF = table.Column<string>(type: "NVARCHAR2(11)", maxLength: 11, nullable: false),
                TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                SENHA_HASH = table.Column<string>(type: "NVARCHAR2(255)", maxLength: 255, nullable: false),
                PERFIL = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                CODIGO_BO = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: true),
                PIN_DISFARCE = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: true),
                POSSUI_MEDIDA_PROTETIVA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                PERMISSAO_MICROFONE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                PERMISSAO_LOCALIZACAO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                ATIVO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                CRIADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                ATUALIZADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TB_VALKY_USUARIOS", x => x.ID);
            });

        migrationBuilder.CreateTable(
            name: "TB_VALKY_ATUALIZACOES_STATUS",
            columns: table => new
            {
                ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                    .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                ALERTA_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                STATUS = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                OBSERVACAO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                RESPONSAVEL = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                CRIADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TB_VALKY_ATUALIZACOES_STATUS", x => x.ID);
                table.ForeignKey(
                    name: "FK_TB_VALKY_ATUALIZACOES_STATUS_TB_VALKY_ALERTAS_ALERTA_ID",
                    column: x => x.ALERTA_ID,
                    principalTable: "TB_VALKY_ALERTAS",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TB_VALKY_CONTATOS_CONFIANCA",
            columns: table => new
            {
                ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                    .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                USUARIO_ID = table.Column<int>(type: "NUMBER(10)", nullable: false),
                NOME = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: true),
                PARENTESCO = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                NOTIFICAR_POR_SMS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                NOTIFICAR_POR_APP = table.Column<int>(type: "NUMBER(10)", nullable: false),
                CRIADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TB_VALKY_CONTATOS_CONFIANCA", x => x.ID);
                table.ForeignKey(
                    name: "FK_TB_VALKY_CONTATOS_CONFIANCA_TB_VALKY_USUARIOS_USUARIO_ID",
                    column: x => x.USUARIO_ID,
                    principalTable: "TB_VALKY_USUARIOS",
                    principalColumn: "ID",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TB_VALKY_ATUALIZACOES_STATUS_ALERTA_ID",
            table: "TB_VALKY_ATUALIZACOES_STATUS",
            column: "ALERTA_ID");

        migrationBuilder.CreateIndex(
            name: "UK_VALKY_CONTATOS_USU_TEL",
            table: "TB_VALKY_CONTATOS_CONFIANCA",
            columns: new[] { "USUARIO_ID", "TELEFONE" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VALKY_USUARIOS_BO",
            table: "TB_VALKY_USUARIOS",
            column: "CODIGO_BO");

        migrationBuilder.CreateIndex(
            name: "UK_VALKY_USUARIOS_CPF",
            table: "TB_VALKY_USUARIOS",
            column: "CPF",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UK_VALKY_USUARIOS_EMAIL",
            table: "TB_VALKY_USUARIOS",
            column: "EMAIL",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TB_VALKY_ATUALIZACOES_STATUS");

        migrationBuilder.DropTable(
            name: "TB_VALKY_CONTATOS_CONFIANCA");

        migrationBuilder.DropTable(
            name: "TB_VALKY_ALERTAS");

        migrationBuilder.DropTable(
            name: "TB_VALKY_USUARIOS");
    }
}
