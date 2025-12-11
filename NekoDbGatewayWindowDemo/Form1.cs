using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NekoDbGateway
{
    public partial class Form1 : Form
    {
        DatabaseGateway dg = new DatabaseGateway(new SqlConnectionFactory("Server=SQL5111.site4now.net;Database=db_a45fb2_dmretiradanew;User Id=db_a45fb2_dmretiradanew_admin;Password=##DMRetiradaLab220##;"));

        public Form1()
        {
            InitializeComponent();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            int i = 0;
            rtbOutput.Clear();
            await dg.Read<SqlServerQueryTranslator>(new QueryBuilder().Select("Nome","id_setor", "id_funcionario")
                                                                                   .Top(5)
                                                                                   .From("Funcionarios"),
                                                                                   new Action<Funcionario>((func) => {
                                                                                       this.Invoke(new Action(() => {
                                                                                           rtbOutput.AppendText($"[{i++}]Funcionario:{func.id_funcionario},{func.id_setor},{func.nome}\n");

                                                                                        }));
                                                                                    }));
          
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            rtbOutput.Clear();
            await dg.Read<SqlServerQueryTranslator>(new QueryBuilder().Select("Nome", "id_setor", "id_funcionario")
                                                                                    .Top(5)
                                                                                    .From("Funcionarios"),
                                                                                    new Action<dynamic>((dynamic func) => {
                                                                                        this.Invoke(new Action(() => {
                                                                                            rtbOutput.AppendText($"Funcionario:{func.id_funcionario}, {func.id_setor}, {func["nome"]}\n");

                                                                                        }));
                                                                                    }));
        }
    }

    public class Funcionario
    {
        public int id_funcionario{get;set;}
        public int id_setor{get;set;}
        public string nome { get; set; }
    }
}
