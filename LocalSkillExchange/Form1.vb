Imports System.IO
Imports System.Data.SQLite
Imports Microsoft.Web.WebView2.Core
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class Form1

    Private Async Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        InitializeDatabase()

        Await WebView21.EnsureCoreWebView2Async()

        Dim htmlPath As String =
            Path.Combine(Application.StartupPath, "html\index.html")

        WebView21.Source = New Uri(htmlPath)

        AddHandler WebView21.CoreWebView2.WebMessageReceived,
            AddressOf WebMessageReceived

    End Sub


    Private Sub WebMessageReceived(sender As Object,
                                   e As CoreWebView2WebMessageReceivedEventArgs)

        Dim json As String = e.WebMessageAsJson
        Dim request As JObject = JObject.Parse(json)

        Dim action As String = request("action").ToString()

        Select Case action

            ' ================= REGISTER =================
            Case "registerUser"

                Dim name As String = request("name").ToString()
                Dim area As String = request("area").ToString()

                Using conn As New SQLiteConnection(connectionString)
                    conn.Open()
                    Dim cmd As New SQLiteCommand(
                        "INSERT INTO Users (Name, Area, Credits, Rating)
                         VALUES (@name,@area,10,0)", conn)

                    cmd.Parameters.AddWithValue("@name", name)
                    cmd.Parameters.AddWithValue("@area", area)
                    cmd.ExecuteNonQuery()
                End Using


            ' ================= ADD SKILL =================
            Case "addSkill"

                Using conn As New SQLiteConnection(connectionString)
                    conn.Open()

                    Dim cmd As New SQLiteCommand(
                        "INSERT INTO Skills 
                        (UserID, Title, Category, Description, CreditsRequired)
                        VALUES (@uid,@title,@cat,'',@cred)", conn)

                    cmd.Parameters.AddWithValue("@uid", Convert.ToInt32(request("userId")))
                    cmd.Parameters.AddWithValue("@title", request("title").ToString())
                    cmd.Parameters.AddWithValue("@cat", request("category").ToString())
                    cmd.Parameters.AddWithValue("@cred", Convert.ToInt32(request("credits")))

                    cmd.ExecuteNonQuery()
                End Using


            ' ================= POST GIG =================
            Case "postGig"

                Using conn As New SQLiteConnection(connectionString)
                    conn.Open()

                    Dim cmd As New SQLiteCommand(
                        "INSERT INTO Gigs
                        (PostedBy, Category, Description, CreditsOffered, Status)
                        VALUES (@uid,@cat,@desc,@cred,'Open')", conn)

                    cmd.Parameters.AddWithValue("@uid", Convert.ToInt32(request("userId")))
                    cmd.Parameters.AddWithValue("@cat", request("category").ToString())
                    cmd.Parameters.AddWithValue("@desc", request("description").ToString())
                    cmd.Parameters.AddWithValue("@cred", Convert.ToInt32(request("credits")))

                    cmd.ExecuteNonQuery()
                End Using


            ' ================= GET USERS =================
            Case "getUsers"

                Dim users As New List(Of Object)

                Using conn As New SQLiteConnection(connectionString)
                    conn.Open()

                    Dim cmd As New SQLiteCommand(
                        "SELECT UserID, Name, Credits FROM Users", conn)

                    Dim reader = cmd.ExecuteReader()

                    While reader.Read()
                        users.Add(New With {
                            .UserID = reader("UserID"),
                            .Name = reader("Name"),
                            .Credits = reader("Credits")
                        })
                    End While
                End Using

                Dim responseUsers = JsonConvert.SerializeObject(New With {
                    .type = "users",
                    .users = users
                })

                WebView21.CoreWebView2.PostWebMessageAsJson(responseUsers)


            ' ================= GET GIGS =================
            Case "getGigs"

                Dim gigs As New List(Of Object)

                Using conn As New SQLiteConnection(connectionString)
                    conn.Open()

                    Dim cmd As New SQLiteCommand(
                        "SELECT GigID, PostedBy, Description, CreditsOffered 
                         FROM Gigs WHERE Status='Open'", conn)

                    Dim reader = cmd.ExecuteReader()

                    While reader.Read()
                        gigs.Add(New With {
                            .GigID = reader("GigID"),
                            .PostedBy = reader("PostedBy"),
                            .Description = reader("Description"),
                            .CreditsOffered = reader("CreditsOffered")
                        })
                    End While
                End Using

                Dim responseGigs = JsonConvert.SerializeObject(New With {
                    .type = "gigs",
                    .gigs = gigs
                })

                WebView21.CoreWebView2.PostWebMessageAsJson(responseGigs)


            ' ================= COMPLETE GIG =================
            Case "completeGig"

                Using conn As New SQLiteConnection(connectionString)
                    conn.Open()

                    Dim transaction = conn.BeginTransaction()

                    Dim gigId = Convert.ToInt32(request("gigId"))
                    Dim postedBy = Convert.ToInt32(request("postedBy"))
                    Dim worker = Convert.ToInt32(request("worker"))
                    Dim credits = Convert.ToInt32(request("credits"))

                    ' Deduct
                    Dim cmd1 As New SQLiteCommand(
                        "UPDATE Users SET Credits = Credits - @c WHERE UserID=@u", conn)
                    cmd1.Parameters.AddWithValue("@c", credits)
                    cmd1.Parameters.AddWithValue("@u", postedBy)
                    cmd1.ExecuteNonQuery()

                    ' Add
                    Dim cmd2 As New SQLiteCommand(
                        "UPDATE Users SET Credits = Credits + @c WHERE UserID=@u", conn)
                    cmd2.Parameters.AddWithValue("@c", credits)
                    cmd2.Parameters.AddWithValue("@u", worker)
                    cmd2.ExecuteNonQuery()

                    ' Close gig
                    Dim cmd3 As New SQLiteCommand(
                        "UPDATE Gigs SET Status='Completed' WHERE GigID=@g", conn)
                    cmd3.Parameters.AddWithValue("@g", gigId)
                    cmd3.ExecuteNonQuery()

                    ' Transaction log
                    Dim cmd4 As New SQLiteCommand(
                        "INSERT INTO Transactions
                        (FromUser, ToUser, Credits, Date)
                        VALUES (@f,@t,@c,@d)", conn)

                    cmd4.Parameters.AddWithValue("@f", postedBy)
                    cmd4.Parameters.AddWithValue("@t", worker)
                    cmd4.Parameters.AddWithValue("@c", credits)
                    cmd4.Parameters.AddWithValue("@d", DateTime.Now.ToString())

                    cmd4.ExecuteNonQuery()

                    transaction.Commit()
                End Using


            ' ================= CHART DATA =================
            Case "getChart"

                Dim labels As New List(Of String)
                Dim values As New List(Of Integer)

                Using conn As New SQLiteConnection(connectionString)
                    conn.Open()

                    Dim cmd As New SQLiteCommand(
                        "SELECT Name, Credits FROM Users", conn)

                    Dim reader = cmd.ExecuteReader()

                    While reader.Read()
                        labels.Add(reader("Name").ToString())
                        values.Add(Convert.ToInt32(reader("Credits")))
                    End While
                End Using

                Dim responseChart = JsonConvert.SerializeObject(New With {
                    .type = "chart",
                    .labels = labels,
                    .values = values
                })

                WebView21.CoreWebView2.PostWebMessageAsJson(responseChart)

        End Select

    End Sub

End Class