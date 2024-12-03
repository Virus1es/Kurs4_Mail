import {useNavigate} from "react-router-dom";
import { Menubar } from 'primereact/menubar';
import {useEffect, useLayoutEffect, useRef, useState} from "react";
import { DataTable } from 'primereact/datatable';
import { Column } from 'primereact/column';
import {isNullOrUndef} from "chart.js/helpers";
import {Toast} from "primereact/toast";

export default function ShowHome(){
    // для уведомлений Toast
    const toast = useRef(null);

    // письма из ящика
    const [letters, setLetters] = useState([]);

    // выбранное письмо
    const [selectedLetter, setSelectedLetter] = useState(null);

    const navigate = useNavigate();

    const checkUser = () => {
        if(isNullOrUndef(localStorage.getItem('userCredentials'))) {
            navigate('/login');
        }
    }

    // если пользователь не зашёл в аккаунт заставляем его зайти
    useEffect(() => {
        console.log(isNullOrUndef(localStorage.getItem('userCredentials')));
        checkUser();
    }, []);

    // получаем письма из ящика с указанной папки
    const getLettersByCommand = async (folder) => {
        const credentialsJSON = localStorage.getItem('userCredentials');
        const data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

        const index = parseInt(localStorage.getItem('curUser'));

        const curUser = data[index];

        console.log(curUser);

        try {
            const response = await fetch("http://localhost:5113/mail/GetMails", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({
                    Email: curUser.email,
                    AppPassword: curUser.password,
                    Action: folder,
                }),
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const data = await response.json();
            console.log("Полученные данные:", data);
            setLetters(data);

        } catch (error) {
            console.error("Ошибка:", error);
            toast.current.show({
                severity: 'error',
                summary: 'Ошибка',
                detail: 'Что-то пошло не так :('
            });
        }
    };

    const items = [
        {
            label: 'Входящие',
            icon: 'pi pi-inbox',
            command: () => {
                getLettersByCommand('Input');
            }
        },
        {
            label: 'Отправленные',
            icon: 'pi pi-send',
            command: () => {
                getLettersByCommand('Output');
            }
        },
        {
            label: 'Черновики',
            icon: 'pi pi-book',
            command: () => {
                getLettersByCommand('Drafts');
            }
        },
        {
            label: 'Корзина',
            icon: 'pi pi-trash',
            command: () => {
                getLettersByCommand('Trash');
            }
        }
    ];

    useEffect(() => {
        console.log(
            localStorage.getItem('userCredentials') === JSON.stringify([]));

        !isNullOrUndef(localStorage.getItem('userCredentials')) && getLettersByCommand('Input');
    }, []);

    const onRowSelect = (event) => {
        toast.current.show({
            severity: 'info',
            summary: 'Product Selected',
            detail: `Name: ${event.data.name}`,
            life: 3000
        });
    };

    return(
        <div>
            <Menubar model={items}
                     style={{width: '100%'}}
            />
            <DataTable value={letters}
                       tableStyle={{ minWidth: '60rem' }}
                       style={{width: '100%'}}
                       emptyMessage={"Папка пуста :("}
                       stripedRows
                       selectionMode="single"
                       selection={selectedLetter}
                       onSelectionChange={(e) => setSelectedLetter(e.value)} dataKey="id"
                       onRowSelect={onRowSelect}
            >
                <Column field="from" header="Отправитель"></Column>
                <Column field="subject" header="Тема"></Column>
                <Column field="date" header="Дата"></Column>
            </DataTable>
            <Toast ref={toast} />
        </div>
    );
}