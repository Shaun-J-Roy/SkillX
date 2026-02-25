Imports System.Data.SQLite
Imports System.IO

Module DatabaseHelper

    Public connectionString As String =
        "Data Source=" & Application.StartupPath & "\skills.db;Version=3;"

    Public Sub InitializeDatabase()

        If Not File.Exists(Application.StartupPath & "\skills.db") Then
            SQLiteConnection.CreateFile(Application.StartupPath & "\skills.db")
        End If

        Using conn As New SQLiteConnection(connectionString)
            conn.Open()
            Dim cmd As New SQLiteCommand(conn)

            ' USERS
            cmd.CommandText =
                "CREATE TABLE IF NOT EXISTS Users(
                    UserID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT,
                    Area TEXT,
                    Credits INTEGER,
                    Rating REAL
                );"
            cmd.ExecuteNonQuery()

            ' SKILLS
            cmd.CommandText =
                "CREATE TABLE IF NOT EXISTS Skills(
                    SkillID INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserID INTEGER,
                    Title TEXT,
                    Category TEXT,
                    Description TEXT,
                    CreditsRequired INTEGER
                );"
            cmd.ExecuteNonQuery()

            ' GIGS
            cmd.CommandText =
                "CREATE TABLE IF NOT EXISTS Gigs(
                    GigID INTEGER PRIMARY KEY AUTOINCREMENT,
                    PostedBy INTEGER,
                    Category TEXT,
                    Description TEXT,
                    CreditsOffered INTEGER,
                    Status TEXT
                );"
            cmd.ExecuteNonQuery()

            ' TRANSACTIONS
            cmd.CommandText =
                "CREATE TABLE IF NOT EXISTS Transactions(
                    TransactionID INTEGER PRIMARY KEY AUTOINCREMENT,
                    FromUser INTEGER,
                    ToUser INTEGER,
                    Credits INTEGER,
                    Date TEXT
                );"
            cmd.ExecuteNonQuery()

        End Using

    End Sub

End Module