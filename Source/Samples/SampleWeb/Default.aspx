<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Exceptionless.SampleWeb._Default" %>
<%@ Import Namespace="System" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
    <head runat="server">
        <title>Test Web Page</title>
    </head>
    <body>
        <form id="form1" runat="server">
            <asp:ScriptManager runat="server">
                <Services>
                    <asp:ServiceReference Path="~/TestService.svc" />
                </Services>
            </asp:ScriptManager>

            <div>
                <asp:Button ID="ErrorButton" runat="server" Text="Error" onclick="ErrorButton_Click" />
                <br />
                <a href="requestinfo.ashx">Test</a>
                <br />
                <a href="Default2.aspx">Test for duplicate</a>
                <br />
                <asp:label id="mylabel" runat="server" />
                <br/>

                <a href="#" onclick="return OnClientClient()">Test WCF exception</a>
                <script type="text/javascript">
                    function OnClientClient() {
                        var service = new TestName.TestService();
                        service.DoWork();
                        return false;
                    }
                </script>
            </div>
        </form>
    </body>
</html>